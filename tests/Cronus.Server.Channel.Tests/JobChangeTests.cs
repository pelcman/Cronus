using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// A script's <c>player.changeJob</c> takes the oracle's advancement path (MapleCharacter.changeJob):
/// the job, the advancement SP (1, +2 for a 4th-tier job, first-job catch-up of 3 per level past
/// 10), the job's max HP/MP bonus with a refill, and a StatChanged the client hears. The stat
/// distribution is untouched unless the script asks for <c>resetStatsForJob</c> (the Cygnus / Aran
/// first-job reset), which returns every point above the job's base to AP.
/// </summary>
public class JobChangeTests
{
    private const int Npc = 9010001;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class Bot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _awaitedBit;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public Bot(int characterId, int awaitedBit)
        {
            _characterId = characterId;
            _awaitedBit = awaitedBit;
        }

        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Masks { get; } = new();

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSelectNpc), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(Npc);
                w.WriteShort(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte(); // unlock
                int mask = p.ReadInt();
                Masks.Add(mask);
                if ((mask & _awaitedBit) != 0)
                {
                    Done.TrySetResult();
                }
            }
        }
    }

    private static async Task<Character> RunScriptAsync(Character hero, string body, int awaitedBit)
    {
        var repo = new InMemoryCharacterRepository();
        hero = repo.Create(hero);
        var scripts = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>
        {
            [Npc] = "function start() { " + body + " }",
        }));

        var bot = new Bot(hero.Id, awaitedBit);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Done.Task.WaitAsync(cts.Token);
        Assert.Contains(bot.Masks, m => (m & awaitedBit) != 0);
        return hero;
    }

    [Fact]
    public async Task FirstJobLate_GrantsCatchUpSp_AndTheWarriorHpBonus()
    {
        var hero = new Character { AccountId = 1, WorldId = 0, Name = "Noble", MapId = 100000000, Level = 12, Job = 1000, Sp = 0, MaxHp = 100, Hp = 30, MaxMp = 50, Mp = 10 };
        hero = await RunScriptAsync(hero, "player.changeJob(1100);", (int)StatFlag.Job);

        Assert.Equal(1100, hero.Job);
        Assert.Equal(1 + 3 * (12 - 10), hero.Sp);
        Assert.InRange(hero.MaxHp, 300, 350);      // +200..250 (the oracle's Soul Master / Warrior range)
        Assert.Equal(hero.MaxHp, hero.Hp);          // refilled
        Assert.Equal(50, hero.MaxMp);
        Assert.Equal(hero.MaxMp, hero.Mp);
    }

    [Fact]
    public async Task FourthTierJob_GrantsThreeSp_AndNoHpBonusOutsideTheTable()
    {
        var hero = new Character { AccountId = 1, WorldId = 0, Name = "Knight", MapId = 100000000, Level = 120, Job = 1111, Sp = 0, MaxHp = 5000, Hp = 5000 };
        hero = await RunScriptAsync(hero, "player.changeJob(1112);", (int)StatFlag.Job);
        Assert.Equal(1112, hero.Job);
        Assert.Equal(3, hero.Sp);
        Assert.Equal(5000, hero.MaxHp);
    }

    [Fact]
    public async Task MagicianFirstJob_GrantsMpAndCountsCatchUpFromLevelEight()
    {
        var hero = new Character { AccountId = 1, WorldId = 0, Name = "Mage", MapId = 100000000, Level = 8, Job = 0, Sp = 0, MaxHp = 60, Hp = 60, MaxMp = 20, Mp = 5 };
        hero = await RunScriptAsync(hero, "player.changeJob(200);", (int)StatFlag.Job);
        Assert.Equal(200, hero.Job);
        Assert.Equal(1, hero.Sp);
        Assert.InRange(hero.MaxMp, 120, 170);
        Assert.Equal(hero.MaxMp, hero.Mp);
    }

    [Fact]
    public async Task ResetStatsForJob_ReturnsEverythingAboveTheBaseToAp()
    {
        var hero = new Character { AccountId = 1, WorldId = 0, Name = "Reset", MapId = 100000000, Level = 12, Job = 1000, Str = 30, Dex = 10, Int = 4, Luk = 4, Ap = 0 };
        hero = await RunScriptAsync(hero, "player.changeJob(1100); player.resetStatsForJob();", (int)StatFlag.Ap);
        Assert.Equal(25, hero.Str);
        Assert.Equal(4, hero.Dex);
        Assert.Equal(4, hero.Int);
        Assert.Equal(4, hero.Luk);
        Assert.Equal(48 - 37, hero.Ap);
    }

    [Theory]
    [InlineData(100, 200, 250, 0, 0)]
    [InlineData(110, 300, 350, 0, 0)]
    [InlineData(210, 0, 0, 400, 450)]
    [InlineData(300, 100, 150, 25, 50)]
    [InlineData(410, 450, 550, 0, 0)]
    [InlineData(1200, 0, 0, 0, 0)]
    public void JobGains_FollowTheOracleTable(int job, int hpMin, int hpMax, int mpMin, int mpMax)
    {
        for (int i = 0; i < 50; i++)
        {
            (int hp, int mp) = ChannelHandler.JobAdvancementGains(job);
            Assert.InRange(hp, hpMin, hpMax);
            Assert.InRange(mp, mpMin, mpMax);
        }
    }

    [Theory]
    [InlineData(1000, 10, 0)]
    [InlineData(1100, 10, 1)]
    [InlineData(1100, 15, 16)]
    [InlineData(200, 8, 1)]
    [InlineData(200, 10, 7)]
    [InlineData(110, 30, 1)]
    [InlineData(111, 70, 1)]
    [InlineData(112, 120, 3)]
    [InlineData(2100, 10, 1)]
    public void JobSp_FollowsTheOracle(int job, int level, int sp)
        => Assert.Equal(sp, ChannelHandler.JobAdvancementSp(job, level));
}
