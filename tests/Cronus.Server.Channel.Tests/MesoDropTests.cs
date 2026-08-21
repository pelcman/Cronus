using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class MesoDropTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void DropEnterFieldMeso_PlayerDrop_FlagsPlayerOrigin()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var field = new Field(100000000);
        FieldDrop playerDrop = field.AddPlayerMesoDrop(100, x: 30, y: 40, sourceCharacterId: 7);

        var r = new PacketReader(packets.DropEnterFieldMeso(playerDrop), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        r.ReadByte();                    // enter type (ANIMATION)
        r.ReadInt();                     // object id
        r.ReadByte();                    // meso flag
        r.ReadInt();                     // meso
        r.ReadInt();                     // owner
        r.ReadByte();                    // drop type
        r.ReadShort();                   // x
        r.ReadShort();                   // y
        Assert.Equal(7, r.ReadInt());    // source = the dropping character
        r.ReadShort();                   // drop-from x
        r.ReadShort();                   // drop-from y
        r.ReadShort();                   // 0
        Assert.Equal(0, r.ReadByte());   // 0 = player drop (a mob drop is 1)
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

    /// <summary>Bob: waits in the field, picks up any drop, and records the meso he gains.</summary>
    private sealed class Picker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public Picker(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> MesoGained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opDropEnter)
            {
                p.ReadByte();               // enter type
                int dropOid = p.ReadInt();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.DropPickUpRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteShort(0);
                w.WriteShort(0);
                w.WriteInt(dropOid);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();               // unlock
                int mask = p.ReadInt();
                if (mask == 0)
                {
                    return;
                }

                int value = p.ReadInt();
                if ((mask & 0x40000) != 0)  // Meso
                {
                    MesoGained.TrySetResult(value);
                }
            }
        }
    }

    /// <summary>Alice: drops mesos on the ground once she's in a field; records her own meso after.</summary>
    private sealed class Thrower : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _mesos;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _dropped;

        public Thrower(int characterId, int mesos)
        {
            _characterId = characterId;
            _mesos = mesos;
        }

        /// <summary>The meso value from the StatChanged the drop request produces (accept or reject).</summary>
        public TaskCompletionSource<int> MesoAfter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_dropped)
            {
                _dropped = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserDropMoneyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);        // timestamp
                w.WriteInt(_mesos);   // mesos
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();          // unlock
                int mask = p.ReadInt();
                if (mask == 0)
                {
                    return;            // entry updateStat
                }

                int value = p.ReadInt();
                if ((mask & 0x40000) != 0) // Meso
                {
                    MesoAfter.TrySetResult(value);
                }
            }
        }
    }

    [Fact]
    public async Task DropMoney_TransfersMesoToAnotherPlayer()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Meso = 1000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Meso = 0 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Picker(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Thrower(alice.Id, mesos: 250);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int bobMeso = await bobClient.MesoGained.Task.WaitAsync(cts.Token);
        Assert.Equal(250, bobMeso);   // Bob picked up Alice's dropped meso
        Assert.Equal(250, bob.Meso);
        Assert.Equal(750, alice.Meso); // 1000 - 250
    }

    [Fact]
    public async Task DropMoney_BelowMinimum_IsRejected()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Meso = 1000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var aliceClient = new Thrower(alice.Id, mesos: 5); // below the 10-meso minimum
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        // The reject path resyncs meso; wait for that, then confirm nothing was spent or dropped.
        int mesoAfter = await aliceClient.MesoAfter.Task.WaitAsync(cts.Token);
        Assert.Equal(1000, mesoAfter);
        Assert.Equal(1000, alice.Meso);
        Assert.Empty(fields.Get(100000000).Drops);
    }
}
