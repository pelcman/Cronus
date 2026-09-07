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
/// /gmmove over the wire: turning it on sends the Speed/Jump/Flying temporary stats (Flying is CTS
/// bit 80, so the 128-bit mask's word[2] carries it), hits then cost no HP, and turning it off
/// resets the same mask and makes hits hurt again.
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
        await client.ChatAsync("/gmmove on");

        StatSet set = await client.Set.Task.WaitAsync(cts.Token);
        Assert.Equal(0u, set.Words[0]);                                                 // word[3]
        Assert.Equal(1u << (BuffEffect.Flying - 64), set.Words[1]);                     // word[2]: Flying = bit 80
        Assert.Equal(0u, set.Words[2]);                                                 // word[1]
        Assert.Equal((1u << BuffEffect.Speed) | (1u << BuffEffect.Jump), set.Words[3]); // word[0]
        Assert.Equal(new[] { (200, 1026), (80, 1026), (1, 1026) }, set.Entries.Select(e => ((int)e.Value, e.Reason)).ToArray());
        Assert.All(set.Entries, e => Assert.Equal(86_400_000, e.Duration));

        // A hit lands on the wire, but HP stays put and no StatChanged follows (the entry sends one).
        int statChangesBefore = client.StatChanges;
        await client.HitAsync(120);
        await Task.Delay(300, cts.Token);
        Assert.Equal(500, hero.Hp);
        Assert.Equal(statChangesBefore, client.StatChanges);

        await client.ChatAsync("/gmmove off");
        uint[] reset = await client.Reset.Task.WaitAsync(cts.Token);
        Assert.Equal(set.Words, reset);

        // Mortal again.
        await client.HitAsync(120);
        while (hero.Hp == 500)
        {
            await Task.Delay(10, cts.Token);
        }

        Assert.Equal(380, hero.Hp);
    }
}
