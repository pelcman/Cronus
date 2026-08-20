using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Mob control: on entry the client is made a mob's controller (LP_MobChangeController);
/// its CP_MobMove gets an LP_MobCtrlAck and the raw path relays as LP_MobMove to others;
/// disconnecting hands control to a remaining player.
/// </summary>
public class MobControlTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class ControlClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opController = ServerOps.Get(ServerOpcode.MobChangeController);
        private readonly int _opCtrlAck = ServerOps.Get(ServerOpcode.MobCtrlAck);
        private readonly int _opMobMove = ServerOps.Get(ServerOpcode.MobMove);

        public ControlClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<bool> EnteredGame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(byte Level, int MobOid, int Template)> GotControl { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int MobOid, short MoveId)> Acked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int MobOid, byte[] Path)> SawMobMove { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = New(session, ClientOpcode.MigrateIn);
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
                EnteredGame.TrySetResult(true);
            }
            else if (opcode == _opController)
            {
                byte level = p.ReadByte();
                int oid = p.ReadInt();
                p.ReadByte();            // calc damage index
                int template = p.ReadInt();
                GotControl.TrySetResult((level, oid, template));
            }
            else if (opcode == _opCtrlAck)
            {
                int oid = p.ReadInt();
                short moveId = p.ReadShort();
                Acked.TrySetResult((oid, moveId));
            }
            else if (opcode == _opMobMove)
            {
                int oid = p.ReadInt();
                p.Skip(2);               // notForceLanding, notChangeAction
                p.ReadBool();            // next attack possible
                p.ReadByte();            // left
                p.ReadInt();             // skill
                p.Skip(8);               // JMS >= 186 pair
                SawMobMove.TrySetResult((oid, p.ReadRemaining()));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask SendMobMoveAsync(int mobOid, short moveId, byte[] rawPath)
        {
            var w = New(Session!, ClientOpcode.MobMove);
            w.WriteInt(mobOid);
            w.WriteShort(moveId);
            w.WriteByte(0);            // next attack possible
            w.WriteByte(0);            // left
            w.WriteInt(0);             // mob skill
            w.WriteInt(0);             // JMS >= 186 pair
            w.WriteInt(0);
            w.WriteByte(0);            // unk2
            w.WriteInt(1);             // unk3
            w.WriteInt(0x00FFDDCC);    // magic pair
            w.WriteInt(0x00FFDDCC);
            w.WriteInt(0);             // trailing int
            w.WriteBytes(rawPath);
            await Session!.SendAsync(w.ToArray());
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    private static FieldRegistry MobField(out MapData map)
    {
        map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 10, Y = 20, MaxHp = 500 } },
        };
        return new FieldRegistry(new InMemoryMapProvider(new[] { map }));
    }

    private static (MapleSession Server, MapleSession Client) Wire(
        ControlClient client, ChannelHandler handler, CancellationToken ct)
    {
        var c2s = new Pipe();
        var s2c = new Pipe();
        var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(ct);
        _ = clientSession.RunAsync(ct);
        return (server, clientSession);
    }

    [Fact]
    public async Task FirstPlayer_BecomesMobController()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Ctrl", MapId = 100000000 });
        FieldRegistry fields = MobField(out _);

        var client = new ControlClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession s, MapleSession c) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = s;
        await using MapleSession s2 = c;

        (byte level, int oid, int template) = await client.GotControl.Task.WaitAsync(cts.Token);

        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);
        Assert.Equal(1, level);                 // control without aggro
        Assert.Equal(mob.ObjectId, oid);
        Assert.Equal(100100, template);
        Assert.Equal(hero.Id, mob.ControllerId);
    }

    [Fact]
    public async Task ControllerMove_GetsAck_AndRelays()
    {
        var repo = new InMemoryCharacterRepository();
        Character controller = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Driver", MapId = 100000000 });
        Character bystander = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Extra", MapId = 100000000 });
        FieldRegistry fields = MobField(out _);

        using var cts = new CancellationTokenSource(Timeout);

        var first = new ControlClient(controller.Id);
        (MapleSession fs, MapleSession fc) = Wire(first, new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields), cts.Token);
        await using MapleSession a = fs;
        await using MapleSession b = fc;
        await first.GotControl.Task.WaitAsync(cts.Token);

        var second = new ControlClient(bystander.Id);
        (MapleSession ss, MapleSession sc) = Wire(second, new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields), cts.Token);
        await using MapleSession d = ss;
        await using MapleSession e = sc;
        await second.EnteredGame.Task.WaitAsync(cts.Token);

        FieldMob mob = fields.Get(100000000).Mobs[0];
        byte[] path = { 0x64, 0x00, 0x2C, 0x01, 0xAB, 0xCD }; // origin (100, 300) + opaque tail
        await first.SendMobMoveAsync(mob.ObjectId, moveId: 7, path);

        (int ackOid, short ackMove) = await first.Acked.Task.WaitAsync(cts.Token);
        Assert.Equal(mob.ObjectId, ackOid);
        Assert.Equal(7, ackMove);

        (int seenOid, byte[] relayed) = await second.SawMobMove.Task.WaitAsync(cts.Token);
        Assert.Equal(mob.ObjectId, seenOid);
        Assert.Equal(path, relayed);

        Assert.Equal(100, mob.X); // position tracked from the path origin
        Assert.Equal(300, mob.Y);
    }

    [Fact]
    public async Task Disconnect_HandsControlToRemainingPlayer()
    {
        var repo = new InMemoryCharacterRepository();
        Character controller = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Leaver", MapId = 100000000 });
        Character heir = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Heir", MapId = 100000000 });
        FieldRegistry fields = MobField(out _);

        using var cts = new CancellationTokenSource(Timeout);

        var first = new ControlClient(controller.Id);
        (MapleSession fs, MapleSession fc) = Wire(first, new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields), cts.Token);
        await first.GotControl.Task.WaitAsync(cts.Token);

        var second = new ControlClient(heir.Id);
        (MapleSession ss, MapleSession sc) = Wire(second, new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields), cts.Token);
        await using MapleSession d = ss;
        await using MapleSession e = sc;
        await second.EnteredGame.Task.WaitAsync(cts.Token);

        // First (controller) disconnects; the heir must receive control.
        await fs.DisposeAsync();
        await fc.DisposeAsync();

        (byte level, int oid, int _) = await second.GotControl.Task.WaitAsync(cts.Token);
        FieldMob mob = fields.Get(100000000).Mobs[0];
        Assert.Equal(1, level);
        Assert.Equal(mob.ObjectId, oid);
        Assert.Equal(heir.Id, mob.ControllerId);
    }
}
