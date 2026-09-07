using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// /gmmove over the wire: turning it on with multipliers sends the Speed/Jump temporary stats,
/// hits then cost no HP, and turning it off resets the same mask and makes hits hurt again.
/// </summary>
public class GmMoveTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record StatSet(uint[] Words, List<(short Value, int Reason, int Duration)> Entries);

    private sealed class GmClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opSet = ServerOps.Get(ServerOpcode.TemporaryStatSet);
        private readonly int _opReset = ServerOps.Get(ServerOpcode.TemporaryStatReset);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public GmClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }

        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<StatSet> Set { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<uint[]> Reset { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StatChanges { get; private set; }

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Entered.TrySetResult(true);
            }
            else if (opcode == _opSet)
            {
                uint[] words = { (uint)p.ReadInt(), (uint)p.ReadInt(), (uint)p.ReadInt(), (uint)p.ReadInt() }; // word[3..0]
                var entries = new List<(short, int, int)>();
                while (p.Remaining > 5)
                {
                    entries.Add((p.ReadShort(), p.ReadInt(), p.ReadInt()));
                }

                Set.TrySetResult(new StatSet(words, entries));
            }
            else if (opcode == _opReset)
            {
                Reset.TrySetResult(new[] { (uint)p.ReadInt(), (uint)p.ReadInt(), (uint)p.ReadInt(), (uint)p.ReadInt() });
            }
            else if (opcode == _opStat)
            {
                StatChanges++;
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask ChatAsync(string message)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);
            w.WriteString(message);
            w.WriteBool(false);
            await Session.SendAsync(w.ToArray());
        }

        /// <summary>Reports a plain mob hit (attack index -1, no attacker block) for <paramref name="damage"/>.</summary>
        public async ValueTask HitAsync(int damage)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserHit), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);              // time
            w.WriteByte(0xFF);          // nAttackIdx -1
            w.WriteByte(0);             // nMagicElemAttr
            w.WriteInt(damage);
            await Session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task GmMove_SendsFlyingStats_BlocksDamage_AndResetsOnOff()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Gm", MapId = 100000000, Hp = 500, MaxHp = 500, Mp = 100, MaxMp = 100 });
        var client = new GmClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Entered.Task.WaitAsync(cts.Token);
        await client.ChatAsync("/gmmove 3 1.8 2");

        StatSet set = await client.Set.Task.WaitAsync(cts.Token);
        Assert.Equal(0u, set.Words[0]);                                                 // word[3]
        Assert.Equal(0u, set.Words[1]);                                                 // word[2]
        Assert.Equal(0u, set.Words[2]);                                                 // word[1]
        Assert.Equal((1u << BuffEffect.Speed) | (1u << BuffEffect.Jump) | (1u << BuffEffect.Booster), set.Words[3]); // word[0]
        // 3x / 1.8x / 2x: frame time (d+10)/16 = 1/2 -> degree -2; no weapon -> speed 6 -> Booster -8.
        // Reasons: Haste for Speed/Jump, a booster skill for Booster — never the soaring skill 1026,
        // which would make the client fly.
        Assert.Equal(new[] { (200, ChannelHandler.GmMoveReasonSkill), (80, ChannelHandler.GmMoveReasonSkill), (-8, ChannelHandler.GmMoveBoosterReasonSkill) },
            set.Entries.Select(e => ((int)e.Value, e.Reason)).ToArray());
        Assert.All(set.Entries, e => Assert.NotEqual(1026, e.Reason));
        Assert.All(set.Entries, e => Assert.Equal(86_400_000, e.Duration));

        // A hit lands on the wire, but HP stays put and no StatChanged follows (the entry sends one).
        int statChangesBefore = client.StatChanges;
        await client.HitAsync(120);
        await Task.Delay(300, cts.Token);
        Assert.Equal(500, hero.Hp);
        Assert.Equal(statChangesBefore, client.StatChanges);

        await client.ChatAsync("/gmmove off");
        uint[] reset = await client.Reset.Task.WaitAsync(cts.Token);
        Assert.Equal(set.Words, reset);                                                  // Speed|Jump|Booster

        // Mortal again.
        await client.HitAsync(120);
        while (hero.Hp == 500)
        {
            await Task.Delay(10, cts.Token);
        }

        Assert.Equal(380, hero.Hp);
    }

    [Theory]
    [InlineData(1.0, 6, 0)]      // unchanged
    [InlineData(2.0, 6, -8)]     // degree -2 for a speed-6 weapon
    [InlineData(2.0, 4, -6)]     // a fast weapon needs less
    [InlineData(4.0, 6, -12)]    // 16/4 - 10 = -6
    [InlineData(8.0, 6, -14)]    // 16/8 - 10 = -8, the floor
    [InlineData(100.0, 6, -14)]  // never below the floor
    [InlineData(1.2, 2, 0)]      // 16/1.2 - 10 = 3.3 -> 3, a speed-2 weapon is already faster: no slowdown
    public void GmMoveBooster_FollowsTheClientsFrameTimeLaw(double times, int weaponSpeed, int expected)
        => Assert.Equal(expected, ChannelHandler.GmMoveBooster(times, weaponSpeed));
}
