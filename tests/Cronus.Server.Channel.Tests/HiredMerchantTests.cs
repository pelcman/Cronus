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

public class HiredMerchantTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private const int FmMap = 910000001;
    private const int PermitItem = 5030000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Theory]
    [InlineData(50_000, 0)]
    [InlineData(100_000, 400)]          // 0.4%
    [InlineData(1_000_000, 9_000)]      // 0.9%
    [InlineData(10_000_000, 200_000)]   // 2%
    [InlineData(100_000_000, 3_000_000)] // 3%
    public void Tax_MatchesReferenceBrackets(int meso, int expected)
        => Assert.Equal(expected, HiredMerchant.Tax(meso));

    [Fact]
    public void Registry_TracksOwnerManagerAndVisitors()
    {
        var registry = new HiredMerchantRegistry();
        var owner = new Character { Id = 1, Name = "Alice", MapId = FmMap };
        var ownerPlayer = new FieldPlayer(owner, session: null!);
        var visitor = new FieldPlayer(new Character { Id = 2, Name = "Bob", MapId = FmMap }, session: null!);

        HiredMerchant m = registry.Create(owner, "store", PermitItem, FmMap, 10, 20, 0);
        Assert.Same(m, registry.Get(m.ObjectId));
        Assert.Same(m, registry.GetByOwner(1));

        registry.SetManager(m, ownerPlayer);
        Assert.Equal(0, m.SeatOf(1));
        Assert.Same(m, registry.GetForParticipant(1));

        registry.RemoveManager(m);
        Assert.Equal(-1, m.SeatOf(1));

        registry.SetVisitor(m, 1, visitor);
        Assert.Equal(1, m.SeatOf(2));

        registry.Remove(m);
        Assert.Null(registry.Get(m.ObjectId));
        Assert.Null(registry.GetForParticipant(2));
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

    private static byte[] MiniRoomPacket(MapleSession session, Action<PacketWriter> body)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MiniRoom), session.Config.PacketHeaderSize, session.Config.CodePage);
        body(w);
        return w.ToArray();
    }

    /// <summary>Creates the merchant, stocks one listing, opens it, and later re-enters to close.</summary>
    private sealed class MerchantOwner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private bool _created;
        private bool _stocked;

        public MerchantOwner(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource Live { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int SoldCount, int Banked)> ManagementView { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            await session.SendAsync(MigrateIn(session, _characterId));
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_created)
            {
                _created = true;
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(0);            // MRP_Create
                    w.WriteByte(5);            // entrusted shop
                    w.WriteString("day one");
                    w.WriteByte(0);
                    w.WriteShort(1);           // permit at cash slot 1
                    w.WriteInt(PermitItem);
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5 && !_stocked) // stocking view opened -> list 2 bundles of 10 at 500
                {
                    _stocked = true;
                    await session.SendAsync(MiniRoomPacket(session, w =>
                    {
                        w.WriteByte(30);       // ESP_PutItem
                        w.WriteByte(2);        // USE tab
                        w.WriteShort(1);
                        w.WriteShort(2);
                        w.WriteShort(10);
                        w.WriteInt(500);
                    }));
                }
                else if (op == 22 && !Live.Task.IsCompleted) // listings ack -> go live
                {
                    await session.SendAsync(MiniRoomPacket(session, w => w.WriteByte(11)));
                    Live.TrySetResult();
                }
                else if (op == 5 && _stocked) // the management view on re-entry
                {
                    p.ReadByte();              // room type
                    p.ReadByte();              // max size
                    p.ReadShort();             // my seat (0)
                    p.ReadInt();               // permit item
                    p.ReadString();            // "雇用商人"
                    p.ReadByte();              // visitor terminator (none kicked yet in view)
                    p.ReadShort();
                    p.ReadString();            // owner name
                    p.ReadInt();               // uptime
                    p.ReadByte();              // firstTime
                    int soldCount = p.ReadByte();
                    for (int i = 0; i < soldCount; i++)
                    {
                        p.ReadInt();
                        p.ReadShort();
                        p.ReadInt();
                        p.ReadString();
                    }

                    ManagementView.TrySetResult((soldCount, p.ReadInt()));
                }
            }
        }
    }

    /// <summary>Waits for the employee NPC, browses it, and buys one bundle.</summary>
    private sealed class MerchantShopper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEmployee = ServerOps.Get(ServerOpcode.EmployeeEnterField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private bool _entered;
        private bool _bought;

        public MerchantShopper(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<short> Bought { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            await session.SendAsync(MigrateIn(session, _characterId));
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opEmployee && !_entered)
            {
                _entered = true;
                p.ReadInt();          // owner id
                p.ReadInt();          // permit look
                p.ReadShort();        // x
                p.ReadShort();        // y
                p.ReadShort();        // fh
                p.ReadString();       // owner name
                p.ReadByte();         // room type
                int objectId = p.ReadInt();
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(4);   // MRP_Enter
                    w.WriteInt(objectId);
                    w.WriteByte(0);
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5 && !_bought)
                {
                    _bought = true;
                    await session.SendAsync(MiniRoomPacket(session, w =>
                    {
                        w.WriteByte(31); // ESP_BuyItem
                        w.WriteByte(0);
                        w.WriteShort(1);
                    }));
                }
                else if (op == 22) // refreshed listings after the buy: [0:4][count][bundles...]
                {
                    p.ReadInt();
                    p.ReadByte();
                    Bought.TrySetResult(p.ReadShort());
                }
            }
        }
    }

    [Fact]
    public async Task Merchant_StockOpenBuyManage_FullLifecycle()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = FmMap });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = FmMap, Meso = 10_000 });
        alice.EquippedItems.Add(new InventoryItem { ItemId = PermitItem, Position = 1, Quantity = 1, CharacterId = alice.Id });
        alice.EquippedItems.Add(new InventoryItem { ItemId = 2060000, Position = 1, Quantity = 20, CharacterId = alice.Id });

        var map = new MapData { MapId = FmMap, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var merchants = new HiredMerchantRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new MerchantShopper(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, merchants: merchants);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new MerchantOwner(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, merchants: merchants);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        await aliceClient.Live.Task.WaitAsync(cts.Token);

        short bundlesLeft = await bobClient.Bought.Task.WaitAsync(cts.Token);
        Assert.Equal(1, bundlesLeft);
        Assert.Equal(10_000 - 500, bob.Meso);
        Assert.Equal(10, bob.EquippedItems.Single(i => i.ItemId == 2060000).Quantity);

        HiredMerchant merchant = merchants.GetByOwner(alice.Id)!;
        Assert.Equal(500, merchant.Meso); // below the first tax bracket
        SoldRecord sale = Assert.Single(merchant.Sold);
        Assert.Equal(("Bob", 500), (sale.Buyer, sale.TotalPrice));

        // The owner re-enters for management and sees the sale + the banked meso.
        var enter = MiniRoomPacket(aliceClient.Session!, w =>
        {
            w.WriteByte(4);
            w.WriteInt(merchant.ObjectId);
            w.WriteByte(0);
        });
        await aliceClient.Session!.SendAsync(enter);

        (int soldCount, int banked) = await aliceClient.ManagementView.Task.WaitAsync(cts.Token);
        Assert.Equal(1, soldCount);
        Assert.Equal(500, banked);

        // Reclaim the remaining bundle, then leave: the store packs up and pays out.
        await aliceClient.Session!.SendAsync(MiniRoomPacket(aliceClient.Session!, w =>
        {
            w.WriteByte(35); // ESP_MoveItemToInventory
            w.WriteShort(0);
        }));
        await aliceClient.Session!.SendAsync(MiniRoomPacket(aliceClient.Session!, w => w.WriteByte(10))); // MRP_Leave

        while (merchants.GetByOwner(alice.Id) is not null)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, cts.Token);
        }

        Assert.Equal(500, alice.Meso); // the banked meso paid out
        Assert.Equal(10, alice.EquippedItems.Single(i => i.ItemId == 2060000).Quantity); // reclaimed bundle
    }
}
