using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class DropSpawnTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void DropEnterFieldMeso_OnGround_UsesNoAnimationAndOmitsDropFrom()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var mob = new FieldMob { ObjectId = 2_000_000, TemplateId = 100100, X = 50, Y = 60 };
        var field = new Field(100000000);
        FieldDrop drop = field.AddMesoDrop(100, x: 10, y: 20, mob);

        var onGround = new PacketReader(packets.DropEnterFieldMeso(drop, onGround: true), ServerConfig.Jms186.CodePage);
        onGround.ReadHeader();
        Assert.Equal(2, onGround.ReadByte());        // NO_ANIMATION
        Assert.Equal(drop.ObjectId, onGround.ReadInt());
        Assert.Equal(1, onGround.ReadByte());        // meso flag
        Assert.Equal(100, onGround.ReadInt());       // meso amount
        Assert.Equal(0, onGround.ReadInt());         // owner (FFA)
        Assert.Equal(2, onGround.ReadByte());        // drop type
        Assert.Equal(10, onGround.ReadShort());      // landing x
        Assert.Equal(20, onGround.ReadShort());      // landing y
        Assert.Equal(mob.ObjectId, onGround.ReadInt()); // source
        Assert.Equal(1, onGround.ReadByte());        // not a player drop
        Assert.Equal(0, onGround.ReadByte());
        Assert.Equal(0, onGround.Remaining);         // no drop-from block

        // The animated form carries the extra 6-byte drop-from block, so its body is 6 bytes longer.
        var animatedBody = new PacketReader(packets.DropEnterFieldMeso(drop, onGround: false), ServerConfig.Jms186.CodePage);
        animatedBody.ReadHeader();
        var groundBody = new PacketReader(packets.DropEnterFieldMeso(drop, onGround: true), ServerConfig.Jms186.CodePage);
        groundBody.ReadHeader();
        Assert.Equal(animatedBody.ReadRemaining().Length - 6, groundBody.ReadRemaining().Length);
    }

    private sealed class Entrant : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);

        public Entrant(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(byte EnterType, int Oid, int Meso)> DropSeen { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opDropEnter)
            {
                byte enterType = p.ReadByte();
                int oid = p.ReadInt();
                p.ReadByte();          // meso flag
                int meso = p.ReadInt();
                DropSeen.TrySetResult((enterType, oid, meso));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Entering_ShowsExistingGroundDrops()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Late", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // A meso drop is already on the ground before the player arrives.
        var mob = new FieldMob { ObjectId = 2_000_000, TemplateId = 100100, X = 5, Y = 5 };
        FieldDrop drop = fields.Get(100000000).AddMesoDrop(250, x: 15, y: 25, mob);

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Entrant(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte enterType, int oid, int meso) = await client.DropSeen.Task.WaitAsync(cts.Token);
        Assert.Equal(2, enterType);        // NO_ANIMATION (already on ground)
        Assert.Equal(drop.ObjectId, oid);
        Assert.Equal(250, meso);
    }
}
