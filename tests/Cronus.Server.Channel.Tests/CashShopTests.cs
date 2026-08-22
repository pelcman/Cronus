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

public class CashShopTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void CashShopPackets_HaveExactVanillaLayouts()
    {
        var cs = new CashShopPackets(ServerOps, ServerConfig.Jms186);

        var cash = new PacketReader(cs.QueryCashResult(12345, 678), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.CashShopQueryCashResult), cash.ReadHeader());
        Assert.Equal(12345, cash.ReadInt());
        Assert.Equal(678, cash.ReadInt());
        Assert.Equal(0, cash.Remaining);

        var item = new CashLockerItem { CashId = 0x1122334455L, ItemId = 5000000, Quantity = 1, CommoditySn = 60000038 };
        var buy = new PacketReader(cs.BuyDone(item, accountId: 7), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.CashShopCashItemResult), buy.ReadHeader());
        Assert.Equal(CashShopPackets.ResBuyDone, buy.ReadByte());
        Assert.Equal(0x1122334455L, buy.ReadLong());   // cash id
        Assert.Equal(7L, buy.ReadLong());              // account id
        Assert.Equal(5000000, buy.ReadInt());          // item id
        Assert.Equal(0, buy.ReadInt());
        Assert.Equal((short)1, buy.ReadShort());       // quantity
        buy.ReadBytes(13);                             // owner
        buy.ReadLong();                                // expiration sentinel
        Assert.Equal(60000038L, buy.ReadLong());       // commodity SN
        Assert.Equal(0, buy.Remaining);                // exactly the 55-byte info

        var locker = new PacketReader(
            cs.LoadLockerDone(new[] { item }, accountId: 7, trunkSlots: 4, charSlots: 3, charCount: 2),
            ServerConfig.Jms186.CodePage);
        locker.ReadHeader();
        Assert.Equal(CashShopPackets.ResLoadLockerDone, locker.ReadByte());
        Assert.Equal((short)1, locker.ReadShort());
        locker.ReadBytes(55);
        Assert.Equal((short)4, locker.ReadShort());
        Assert.Equal((short)3, locker.ReadShort());
        Assert.Equal((short)0, locker.ReadShort());
        Assert.Equal((short)2, locker.ReadShort());
        Assert.Equal(0, locker.Remaining);
    }

    [Fact]
    public void WzCommodityProvider_ParsesCatalogEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-comm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Etc"));
        File.WriteAllText(Path.Combine(root, "Etc", "Commodity.img.xml"),
            """
            <imgdir name="Commodity.img"><imgdir name="0"><int name="SN" value="10000001"/><int name="ItemId" value="1002077"/><int name="Count" value="1"/><int name="Price" value="390"/></imgdir><imgdir name="1"><int name="SN" value="60000038"/><int name="ItemId" value="5000000"/><int name="Count" value="1"/><int name="Price" value="4900"/></imgdir></imgdir>
            """);
        try
        {
            var provider = new WzCommodityProvider(root);
            Commodity? pet = provider.GetBySn(60000038);
            Assert.NotNull(pet);
            Assert.Equal(5000000, pet!.ItemId);
            Assert.Equal(4900, pet.Price);
            Assert.Null(provider.GetBySn(999));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Migrates in, buys SN 60000038, then moves it into the inventory.</summary>
    private sealed class Shopper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetShop = ServerOps.Get(ServerOpcode.SetCashShop);
        private readonly int _opCashItem = ServerOps.Get(ServerOpcode.CashShopCashItemResult);
        private bool _bought;

        public Shopper(int characterId) => _characterId = characterId;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<long> Bought { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<short> Moved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetShop && !_bought)
            {
                _bought = true;
                Entered.TrySetResult();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.CashShopCashItemRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0x03);       // CashItemReq_Buy
                w.WriteByte(0);          // nexon points
                w.WriteInt(60000038);    // commodity SN
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opCashItem)
            {
                int type = p.ReadByte();
                if (type == CashShopPackets.ResBuyDone)
                {
                    long cashId = p.ReadLong();
                    Bought.TrySetResult(cashId);

                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.CashShopCashItemRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(0x0E);   // CashItemReq_MoveLtoS
                    w.WriteLong(cashId);
                    w.WriteByte(5);      // cash tab
                    w.WriteShort(0);
                    await session.SendAsync(w.ToArray());
                }
                else if (type == CashShopPackets.ResMoveLtoSDone)
                {
                    Moved.TrySetResult(p.ReadShort()); // landing slot
                }
            }
        }
    }

    [Fact]
    public async Task BuyAndMoveToInventory_EndToEnd()
    {
        var accounts = new InMemoryAccountRepository();
        Account account = accounts.Create("shopper", "pw", 0);
        account.NexonPoint = 10_000;

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = account.Id, WorldId = 0, Name = "Buyer", MapId = 100000000 });

        var commodities = new InMemoryCommodityProvider(new[] { new Commodity(60000038, 5000000, 1, 4900) });
        var client = new Shopper(hero.Id);
        var handler = new CashShopHandler(ClientOps, ServerOps, repo, accounts, ServerConfig.Jms186, commodities);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var session = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = session.RunAsync(cts.Token);

        await client.Entered.Task.WaitAsync(cts.Token);
        long cashId = await client.Bought.Task.WaitAsync(cts.Token);
        Assert.True(cashId != 0);
        Assert.Equal(10_000 - 4900, account.NexonPoint);   // charged
        short slot = await client.Moved.Task.WaitAsync(cts.Token);
        Assert.True(slot > 0);

        // The pet now sits in the character's cash tab with its cash id; the locker is empty.
        InventoryItem? pet = hero.EquippedItems.FirstOrDefault(i => i.ItemId == 5000000);
        Assert.NotNull(pet);
        Assert.Equal(cashId, pet!.CashId);
        Assert.Empty(account.CashLocker);
    }
}
