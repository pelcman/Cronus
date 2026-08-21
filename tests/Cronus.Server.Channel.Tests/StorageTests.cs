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

public class StorageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void TrunkOpen_EmptyStorage_HasExpectedLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var storage = new Storage { Meso = 0 };

        var r = new PacketReader(packets.TrunkOpen(1012003, storage), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(21, r.ReadByte());          // op = OpenTrunkDlg
        Assert.Equal(1012003, r.ReadInt());      // npc id
        Assert.Equal(Storage.DefaultSlots, r.ReadByte()); // slot count
        Assert.Equal(-1L, r.ReadLong());         // DBCHAR mask (all)
        Assert.Equal(0, r.ReadInt());            // stored meso
        Assert.Equal(0, r.ReadByte());           // equip count
        Assert.Equal(0, r.ReadByte());           // use count
        Assert.Equal(0, r.ReadByte());           // setup count
        Assert.Equal(0, r.ReadByte());           // etc count
        Assert.Equal(0, r.ReadByte());           // cash count
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void TrunkMoneyResult_IsMoneyMaskPlusMeso()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var storage = new Storage { Meso = 5000 };

        var r = new PacketReader(packets.TrunkMoneyResult(storage), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(18, r.ReadByte());          // op = MoneySuccess
        Assert.Equal(Storage.DefaultSlots, r.ReadByte());
        Assert.Equal(0x2L, r.ReadLong());        // mask = MONEY only
        Assert.Equal(5000, r.ReadInt());         // stored meso
        Assert.Equal(0, r.Remaining);            // no item categories
    }

    [Fact]
    public void TrunkError_IsJustTheOpCode()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.TrunkError(TrunkOp.PutNoSpace), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(16, r.ReadByte());          // PutNoSpace
        Assert.Equal(0, r.Remaining);            // bodiless
    }

    /// <summary>Opens storage, deposits its potion stack, then withdraws it back.</summary>
    private sealed class Banker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opTrunk = ServerOps.Get(ServerOpcode.TrunkResult);
        private bool _opened;

        public Banker(int characterId) => _characterId = characterId;

        public TaskCompletionSource RoundTripDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_opened)
            {
                _opened = true;
                await Chat(session, "/storage");
            }
            else if (opcode == _opTrunk)
            {
                byte op = p.ReadByte();
                if (op == 21) // OpenTrunkDlg -> deposit the potion in USE slot 1
                {
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserTrunkRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(4);       // TrunkReq_PutItem
                    w.WriteShort(1);      // inventory slot
                    w.WriteInt(2000000);  // item id
                    w.WriteShort(5);      // whole stack
                    await session.SendAsync(w.ToArray());
                }
                else if (op == 12) // PutSuccess -> withdraw it back (USE type, index 0)
                {
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserTrunkRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(3);       // TrunkReq_GetItem
                    w.WriteByte(2);       // USE type
                    w.WriteByte(0);       // storage index 0
                    await session.SendAsync(w.ToArray());
                }
                else if (op == 8) // GetSuccess -> done
                {
                    RoundTripDone.TrySetResult();
                }
            }
        }

        private static async ValueTask Chat(MapleSession session, string text)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(0);
            w.WriteString(text);
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task DepositThenWithdraw_MovesItemAndChargesFeeOnce()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 42, WorldId = 0, Name = "Saver", MapId = 100000000, Meso = 1000 });
        hero.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 5, CharacterId = hero.Id });
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var storages = new StorageRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Banker(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, storages: storages);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.RoundTripDone.Task.WaitAsync(cts.Token);

        // The potion made a full round-trip; only the deposit fee (100) was charged.
        Assert.Empty(storages.Get(42).Items);
        InventoryItem potion = Assert.Single(hero.EquippedItems, i => i.ItemId == 2000000);
        Assert.Equal(5, potion.Quantity);
        Assert.Equal(900, hero.Meso); // 1000 - 100 fee
    }
}
