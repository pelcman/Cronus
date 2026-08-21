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

public class PlayerShopTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private const int FmMap = 910000001;
    private const int PermitItem = 5140000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static FieldPlayer Player(int id, string name, int map = FmMap)
        => new(new Character { Id = id, Name = name, MapId = map }, session: null!);

    [Fact]
    public void Registry_ObjectIds_NeverCollideWithMiniGames()
    {
        var games = new MiniGameRegistry();
        var shops = new PlayerShopRegistry();

        MiniGame game = games.Create(MiniGame.TypeOmok, Player(1, "A"), "g", "", 0);
        PlayerShop shop = shops.Create(Player(2, "B"), "s", PermitItem);

        Assert.NotEqual(game.ObjectId, shop.ObjectId);
        Assert.Null(games.Get(shop.ObjectId));
        Assert.Null(shops.Get(game.ObjectId));
    }

    [Fact]
    public void Shop_SeatsAndSoldOut()
    {
        var shops = new PlayerShopRegistry();
        PlayerShop shop = shops.Create(Player(1, "A"), "s", PermitItem);

        Assert.Equal(0, shop.SeatOf(1));
        Assert.Equal(-1, shop.SeatOf(2));
        Assert.Equal(1, shop.FreeSeat());

        shops.SetVisitor(shop, 1, Player(2, "B"));
        shops.SetVisitor(shop, 2, Player(3, "C"));
        Assert.Equal(3, shop.Size);
        Assert.Equal(3, shop.FreeSeat());
        Assert.Equal(2, shop.SeatOf(3));

        Assert.False(shop.IsSoldOut); // no listings yet
        shop.Items.Add(new PlayerShopItem(new InventoryItem { ItemId = 2000000, Quantity = 10 }, 2, 100));
        Assert.False(shop.IsSoldOut);
        shop.Items[0].Bundles = 0;
        Assert.True(shop.IsSoldOut);
    }

    // ---- e2e: stock -> open -> visitor joins from balloon -> buys -> both sides settle ----

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

    private static byte[] MiniRoomPacket(MapleSession session, Action<PacketWriter> body)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MiniRoom), session.Config.PacketHeaderSize, session.Config.CodePage);
        body(w);
        return w.ToArray();
    }

    /// <summary>Opens a shop, lists 2 bundles of 10 arrows at 500 meso, opens for business.</summary>
    private sealed class ShopOwner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _created;
        private bool _stocked;

        public ShopOwner(int characterId) => _characterId = characterId;

        public TaskCompletionSource OpenForBusiness { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource MesoArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_created)
            {
                _created = true;
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(0);           // MRP_Create
                    w.WriteByte(4);           // personal shop
                    w.WriteString("bargains");
                    w.WriteByte(0);
                    w.WriteShort(1);          // permit at cash slot 1
                    w.WriteInt(PermitItem);
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5 && !_stocked) // our shop opened -> stock it
                {
                    _stocked = true;
                    await session.SendAsync(MiniRoomPacket(session, w =>
                    {
                        w.WriteByte(19);      // PSP_PutItem
                        w.WriteByte(2);       // USE tab
                        w.WriteShort(1);      // arrows at slot 1
                        w.WriteShort(2);      // 2 bundles
                        w.WriteShort(10);     // 10 per bundle
                        w.WriteInt(500);      // 500 meso per bundle
                    }));
                }
                else if (op == 22) // listings ack -> open for business
                {
                    await session.SendAsync(MiniRoomPacket(session, w => w.WriteByte(11)));
                    OpenForBusiness.TrySetResult();
                }
            }
            else if (opcode == _opStat && OpenForBusiness.Task.IsCompleted)
            {
                MesoArrived.TrySetResult(); // the sale credited us
            }
        }
    }

    /// <summary>Joins the shop from its balloon and buys one bundle.</summary>
    private sealed class Shopper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private readonly int _opBalloon = ServerOps.Get(ServerOpcode.UserMiniRoomBalloon);
        private bool _joined;
        private bool _bought;

        public Shopper(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(short Bundles, int Price)> Refreshed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opBalloon && !_joined)
            {
                p.ReadInt();
                byte gameType = p.ReadByte();
                if (gameType != 4)
                {
                    return; // not a shop balloon (e.g. the clear on close)
                }

                _joined = true;
                int objectId = p.ReadInt();
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(4);       // MRP_Enter
                    w.WriteInt(objectId);
                    w.WriteByte(0);
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5 && !_bought) // we're in -> buy one bundle of listing 0
                {
                    _bought = true;
                    await session.SendAsync(MiniRoomPacket(session, w =>
                    {
                        w.WriteByte(20);  // PSP_BuyItem
                        w.WriteByte(0);
                        w.WriteShort(1);
                    }));
                }
                else if (op == 22) // refreshed listings after the buy
                {
                    p.ReadByte();                       // count (1)
                    short bundles = p.ReadShort();
                    p.ReadShort();                      // per bundle
                    int price = p.ReadInt();
                    Refreshed.TrySetResult((bundles, price));
                }
            }
        }
    }

    [Fact]
    public async Task StockOpenBuy_TransfersItemsAndMeso()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = FmMap });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = FmMap, Meso = 10_000 });
        alice.EquippedItems.Add(new InventoryItem { ItemId = PermitItem, Position = 1, Quantity = 1, CharacterId = alice.Id });
        alice.EquippedItems.Add(new InventoryItem { ItemId = 2060000, Position = 1, Quantity = 20, CharacterId = alice.Id }); // arrows (USE)

        var map = new MapData { MapId = FmMap, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var playerShops = new PlayerShopRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Shopper(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, playerShops: playerShops);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new ShopOwner(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, playerShops: playerShops);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        await aliceClient.OpenForBusiness.Task.WaitAsync(cts.Token);

        (short bundlesLeft, int price) = await bobClient.Refreshed.Task.WaitAsync(cts.Token);
        Assert.Equal(1, bundlesLeft);  // one of two bundles sold
        Assert.Equal(500, price);

        await aliceClient.MesoArrived.Task.WaitAsync(cts.Token);
        Assert.Equal(500, alice.Meso);            // the sale price
        Assert.Equal(10_000 - 500, bob.Meso);
        Assert.Equal(10, bob.EquippedItems.Single(i => i.ItemId == 2060000).Quantity); // the bought bundle
        Assert.Equal(0, alice.EquippedItems.Count(i => i.ItemId == 2060000));          // stock left the bag

        PlayerShop shop = playerShops.GetForCharacter(alice.Id)!;
        Assert.Equal(1, shop.Items[0].Bundles);
    }
}
