using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class CharacterInfoTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void CharacterInfo_HasExactVanillaLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var c = new Character { Id = 7, Name = "Bob", Level = 30, Job = 100, Fame = 15 };

        var r = new PacketReader(packets.CharacterInfo(c), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.CharacterInfo), r.ReadHeader());
        Assert.Equal(7, r.ReadInt());       // id
        Assert.Equal(30, r.ReadByte());     // level
        Assert.Equal(100, r.ReadShort());   // job
        Assert.Equal(15, r.ReadShort());    // fame
        Assert.Equal(0, r.ReadByte());      // married
        Assert.Equal("-", r.ReadString());  // community
        Assert.Equal("", r.ReadString());   // alliance
        Assert.Equal(0, r.ReadInt());       // JMS 180-186 pair
        Assert.Equal(0, r.ReadInt());
        Assert.Equal(0, r.ReadByte());      // pet activated
        Assert.Equal(0, r.ReadByte());      // pet info: empty slot 0
        Assert.Equal(0, r.ReadByte());      // taming enabled
        Assert.Equal(0, r.ReadByte());      // wishlist size
        Assert.Equal(1, r.ReadInt());       // monster book: level (1 even when empty, per TacosMonsterBook)
        Assert.Equal(0, r.ReadInt());       // normal
        Assert.Equal(0, r.ReadInt());       // special
        Assert.Equal(0, r.ReadInt());       // total
        Assert.Equal(0, r.ReadInt());       // cover mob
        Assert.Equal(0, r.ReadInt());       // medal id
        Assert.Equal(0, r.ReadShort());     // medal quest count
        Assert.Equal(0, r.ReadInt());       // chair count
        Assert.Equal(0, r.Remaining);       // exact layout, nothing left over
    }

    private static byte[] MigrateIn(MapleSession session, int characterId)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteInt(characterId);
        w.WriteBytes(new byte[16]);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteLong(0);
        return w.ToArray();
    }

    private sealed class Resident : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);

        public Resident(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Requests the target's info on entry and reads back their id/level/job/fame.</summary>
    private sealed class Inspector : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _targetId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInfo = ServerOps.Get(ServerOpcode.CharacterInfo);
        private bool _asked;

        public Inspector(int characterId, int targetId)
        {
            _characterId = characterId;
            _targetId = targetId;
        }

        public TaskCompletionSource<(int Id, int Level, int Job, int Fame)> Info { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_asked)
            {
                _asked = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserCharacterInfoRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);           // update time
                w.WriteInt(_targetId);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInfo)
            {
                int id = p.ReadInt();
                int level = p.ReadByte();
                int job = p.ReadShort();
                int fame = p.ReadShort();
                Info.TrySetResult((id, level, job, fame));
            }
        }
    }

    [Fact]
    public async Task Requesting_AnotherPlayer_ReturnsTheirInfo()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Level = 55, Job = 412, Fame = 21 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Resident(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Inspector(alice.Id, bob.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (int id, int level, int job, int fame) = await aliceClient.Info.Task.WaitAsync(cts.Token);
        Assert.Equal(bob.Id, id);
        Assert.Equal(55, level);
        Assert.Equal(412, job);
        Assert.Equal(21, fame);
    }
}
