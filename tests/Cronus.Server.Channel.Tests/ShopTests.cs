using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class ShopTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // A dump with unrelated tables around the shop tables, to prove parsing is scoped per INSERT.
    private const string Sql = """
        INSERT INTO `drop_data` (`id`, `dropperid`, `itemid`, `minimum_quantity`, `maximum_quantity`, `questid`, `chance`) VALUES
        (1, 100100, 4000019, 1, 1, 0, 1000);
        INSERT INTO `shopitems` (`shopitemid`, `shopid`, `itemid`, `price`, `position`, `reqitem`, `reqitemq`) VALUES
        (1, 11100, 2000001, 160, 2, 0, 0),
        (2, 11100, 2000000, 50, 1, 0, 0),
        (3, 11000, 2070000, 10, 1, 0, 0);
        INSERT INTO `shops` (`shopid`, `npcid`) VALUES
        (11100, 9000000),
        (11000, 9000001);
        """;

    [Fact]
    public void Parse_MapsNpcToShop_InPositionOrder()
    {
        SqlShopProvider provider = SqlShopProvider.Parse(Sql);

        Shop? shop = provider.GetShopByNpc(9000000);
        Assert.NotNull(shop);
        Assert.Equal(11100, shop!.ShopId);
        Assert.Equal(2, shop.Items.Count);
        Assert.Equal(2000000, shop.Items[0].ItemId); // position 1 sorts first
        Assert.Equal(50, shop.Items[0].Price);
        Assert.Equal(2000001, shop.Items[1].ItemId); // position 2

        // The drop_data row must not leak into any shop (scoped parsing).
        Assert.DoesNotContain(shop.Items, i => i.ItemId == 4000019);
    }

    [Fact]
    public void Parse_LookupByShopId_AndUnknownIsNull()
    {
        SqlShopProvider provider = SqlShopProvider.Parse(Sql);
        Assert.Equal(9000001, provider.GetShop(11000)!.NpcId);
        Assert.Null(provider.GetShopByNpc(1234));
        Assert.Null(provider.GetShop(1234));
    }

    [Fact]
    public void OpenShopDlg_NonRechargeableItem_HasExpectedLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var shop = new Shop { ShopId = 100, NpcId = 9000000, Items = new[] { new ShopItem(2000000, 50, 1, 0, 0) } };
        var items = new InMemoryItemProvider(Array.Empty<ConsumeSpec>());

        var r = new PacketReader(packets.OpenShopDlg(shop, items), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(9000000, r.ReadInt());   // npc id
        Assert.Equal((short)1, r.ReadShort()); // item count
        Assert.Equal(2000000, r.ReadInt());   // item id
        Assert.Equal(50, r.ReadInt());        // price
        Assert.Equal(0, r.ReadInt());         // req item
        Assert.Equal(0, r.ReadInt());         // req item qty
        Assert.Equal(0, r.ReadInt());         // period
        Assert.Equal(0, r.ReadInt());         // level limit
        Assert.Equal((short)1, r.ReadShort()); // quantity constant (non-rechargeable)
        Assert.Equal((short)100, r.ReadShort()); // slotMax default
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void OpenShopDlg_Rechargeable_WritesDoubleUnitPrice()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var shop = new Shop { ShopId = 100, NpcId = 9000001, Items = new[] { new ShopItem(2070000, 10, 1, 0, 0) } };
        var items = new InMemoryItemProvider(Array.Empty<ConsumeSpec>(), new Dictionary<int, int> { [2070000] = 5 });

        var r = new PacketReader(packets.OpenShopDlg(shop, items), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        r.ReadInt();                       // npc
        r.ReadShort();                     // count
        r.ReadInt();                       // itemId
        r.ReadInt();                       // price
        r.ReadInt(); r.ReadInt();          // req item / qty
        r.ReadInt(); r.ReadInt();          // period / level
        Assert.Equal(5.0, BitConverter.Int64BitsToDouble(r.ReadLong())); // 8-byte double unit price
        r.ReadShort();                     // slotMax
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void ShopResult_IsSingleCodeByte()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.ShopResult(ShopResultCode.BuySuccess), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.Remaining);
    }

    /// <summary>Migrates in, opens shop 100 via /shop, buys a red potion, records the buy result.</summary>
    private sealed class Shopper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opOpenShop = ServerOps.Get(ServerOpcode.OpenShopDlg);
        private readonly int _opShopResult = ServerOps.Get(ServerOpcode.ShopResult);
        private bool _openedRequested;

        public Shopper(int characterId) => _characterId = characterId;

        public TaskCompletionSource<byte> BuyResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_openedRequested)
            {
                _openedRequested = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString("/shop 100");
                w.WriteByte(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opOpenShop)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserShopRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);        // ShopReq_Buy
                w.WriteShort(0);       // shop position (discarded)
                w.WriteInt(2000000);   // red potion
                w.WriteShort(3);       // buy 3
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opShopResult)
            {
                BuyResult.TrySetResult(p.ReadByte());
            }
        }
    }

    [Fact]
    public async Task Buy_DebitsMesoAndAddsItem()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Buyer", MapId = 100000000, Meso = 1000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        var shops = new InMemoryShopProvider(new[]
        {
            new Shop { ShopId = 100, NpcId = 9000000, Items = new[] { new ShopItem(2000000, 50, 1, 0, 0) } },
        });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Shopper(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, shops: shops);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        byte result = await client.BuyResult.Task.WaitAsync(cts.Token);

        Assert.Equal((byte)ShopResultCode.BuySuccess, result);
        Assert.Equal(850, hero.Meso); // 1000 - 50*3
        InventoryItem potion = Assert.Single(hero.EquippedItems, i => i.ItemId == 2000000);
        Assert.Equal(3, potion.Quantity);
    }
}
