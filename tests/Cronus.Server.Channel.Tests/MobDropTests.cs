using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class MobDropTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void DropEnterFieldItem_CarriesItemFlagAndExpiration()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var mob = new FieldMob { ObjectId = 2_000_001, TemplateId = 100100, X = 50, Y = 60 };
        var field = new Field(100000000);
        FieldDrop drop = field.AddItemDrop(2000000, quantity: 3, x: 10, y: 20, mob);

        var r = new PacketReader(packets.DropEnterFieldItem(drop), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(1, r.ReadByte());               // ANIMATION
        Assert.Equal(drop.ObjectId, r.ReadInt());
        Assert.Equal(0, r.ReadByte());               // meso flag = 0 (item)
        Assert.Equal(2000000, r.ReadInt());          // item id (not the meso field)
        Assert.Equal(0, r.ReadInt());                // owner (FFA)
        Assert.Equal(2, r.ReadByte());               // drop type
        Assert.Equal(10, r.ReadShort());             // landing x
        Assert.Equal(20, r.ReadShort());             // landing y
        Assert.Equal(mob.ObjectId, r.ReadInt());     // source
        Assert.Equal(mob.X, r.ReadShort());          // drop-from x (ANIMATION)
        Assert.Equal(mob.Y, r.ReadShort());          // drop-from y
        Assert.Equal(0, r.ReadShort());
        Assert.Equal(-1L, r.ReadLong());             // 8-byte expiration (item only)
        Assert.Equal(1, r.ReadByte());               // mob drop (not player)
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void DropEnterFieldItem_IsEightBytesLongerThanMeso()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var mob = new FieldMob { ObjectId = 2_000_001, TemplateId = 100100, X = 50, Y = 60 };
        var field = new Field(100000000);
        FieldDrop item = field.AddItemDrop(2000000, quantity: 1, x: 10, y: 20, mob);
        FieldDrop meso = field.AddMesoDrop(100, x: 10, y: 20, mob);

        var itemBody = new PacketReader(packets.DropEnterFieldItem(item), ServerConfig.Jms186.CodePage);
        itemBody.ReadHeader();
        var mesoBody = new PacketReader(packets.DropEnterFieldMeso(meso), ServerConfig.Jms186.CodePage);
        mesoBody.ReadHeader();

        // The only body difference is the item's 8-byte expiration trailer.
        Assert.Equal(mesoBody.ReadRemaining().Length + 8, itemBody.ReadRemaining().Length);
    }

    /// <summary>Waits in the field, picks up the first drop it sees, and flags when its slot updates.</summary>
    private sealed class ItemPicker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);

        public ItemPicker(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InventoryUpdated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                Ready.TrySetResult();
            }
            else if (opcode == _opDropEnter)
            {
                p.ReadByte();            // enter type
                int dropOid = p.ReadInt();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.DropPickUpRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteShort(0);
                w.WriteShort(0);
                w.WriteInt(dropOid);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInvOp)
            {
                InventoryUpdated.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task PickingUpItemDrop_AddsItToInventory()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Looter", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // An item stack of 3 red potions is already lying on the ground.
        var mob = new FieldMob { ObjectId = 2_000_000, TemplateId = 100100, X = 5, Y = 5 };
        FieldDrop drop = fields.Get(100000000).AddItemDrop(2000000, quantity: 3, x: 15, y: 25, mob);

        using var cts = new CancellationTokenSource(Timeout);

        var client = new ItemPicker(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.InventoryUpdated.Task.WaitAsync(cts.Token);

        // The drop left the field and the stack landed in the USE tab.
        Assert.Empty(fields.Get(100000000).Drops);
        InventoryItem item = Assert.Single(hero.EquippedItems, i => i.ItemId == 2000000);
        Assert.Equal(3, item.Quantity);
        Assert.True(item.Position > 0); // a positive (inventory) slot, not equipped
    }
}
