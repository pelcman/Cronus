using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// A script's <c>player.teachSkill</c> sets the learned level on the character and pushes a
/// ChangeSkillRecordResult so the client's skill window updates at once (the oracle's
/// changeSkillLevel); level 0 forgets the skill.
/// </summary>
public class TeachSkillTests
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
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opSkill = ServerOps.Get(ServerOpcode.ChangeSkillRecordResult);

        public Bot(int characterId) => _characterId = characterId;

        public List<(int Skill, int Level, int Master)> Records { get; } = new();
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            else if (opcode == _opSkill)
            {
                p.ReadByte();  // 1
                p.ReadShort(); // record count
                Records.Add((p.ReadInt(), p.ReadInt(), p.ReadInt()));
                if (Records.Count >= 2)
                {
                    Done.TrySetResult();
                }
            }
        }
    }

    [Fact]
    public async Task TeachSkill_SetsTheLevel_AndTheClientHearsTheRecord()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Aran", MapId = 100000000, Level = 20, Job = 2100 });
        hero.Skills[21000001] = 3;
        var scripts = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>
        {
            [Npc] = "function start() { player.teachSkill(21000000, 1); player.teachSkill(21000001, 0); }",
        }));

        var bot = new Bot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Done.Task.WaitAsync(cts.Token);

        Assert.Equal(new[] { (21000000, 1, 0), (21000001, 0, 0) }, bot.Records);
        Assert.Equal(1, hero.Skills[21000000]);
        Assert.False(hero.Skills.ContainsKey(21000001));
    }
}
