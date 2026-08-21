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

public class ItemEquipTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static InventoryItem Equip(int itemId, short pos, int charId) =>
        new() { ItemId = itemId, Position = pos, Quantity = 1, CharacterId = charId };

    [Fact]
    public void Move_Equip_MovesItemToNegativeSlot()
    {
        var c = new Character { Id = 1, Name = "Hero" };
        c.EquippedItems.Add(Equip(1302000, pos: 3, charId: 1)); // a sword in inventory slot 3

        InventoryChange? change = Inventory.Move(c, tab: 1, src: 3, dst: -11);

        Assert.NotNull(change);
        Assert.Equal(InvMode.Move, change!.Value.Mode);
        Assert.Equal((short)3, change.Value.Position);
        Assert.Equal((short)-11, change.Value.DestPosition);
        Assert.Equal((short)-11, Assert.Single(c.EquippedItems).Position); // now equipped
    }

    [Fact]
    public void Move_Equip_SwapsWithAlreadyEquippedItem()
    {
        var c = new Character { Id = 1, Name = "Hero" };
        InventoryItem worn = Equip(1302000, pos: -11, charId: 1);   // sword already equipped
        InventoryItem bag = Equip(1302001, pos: 3, charId: 1);      // another sword in slot 3
        c.EquippedItems.Add(worn);
        c.EquippedItems.Add(bag);

        Inventory.Move(c, tab: 1, src: 3, dst: -11);

        Assert.Equal((short)-11, bag.Position); // the bagged sword is now worn
        Assert.Equal((short)3, worn.Position);  // the old one swapped into slot 3
    }

    [Fact]
    public void Move_EmptySource_ReturnsNull()
    {
        var c = new Character { Id = 1, Name = "Hero" };
        Assert.Null(Inventory.Move(c, tab: 1, src: 5, dst: -11));
    }

    [Fact]
    public void InventoryOperation_Move_EquipChange_HasTrailerByte()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var change = new InventoryChange(InvMode.Move, 1, 3, null, 0, -11);

        var r = new PacketReader(packets.InventoryOperation(new[] { change }), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(1, r.ReadByte());     // unlock
        Assert.Equal(1, r.ReadByte());     // op count
        Assert.Equal(2, r.ReadByte());     // mode = Move
        Assert.Equal(1, r.ReadByte());     // tab
        Assert.Equal((short)3, r.ReadShort());    // source slot
        Assert.Equal((short)-11, r.ReadShort());  // destination (equipped) slot
        Assert.Equal(0, r.ReadByte());     // equip-change trailer byte
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void InventoryOperation_Move_WithinTab_HasNoTrailerByte()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var change = new InventoryChange(InvMode.Move, 2, 1, null, 0, 5); // USE-tab rearrange

        var r = new PacketReader(packets.InventoryOperation(new[] { change }), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        r.ReadByte();                       // unlock
        r.ReadByte();                       // count
        r.ReadByte();                       // mode
        r.ReadByte();                       // tab
        r.ReadShort();                      // src
        r.ReadShort();                      // dst
        Assert.Equal(0, r.Remaining);       // no equip trailer for a positive→positive move
    }

    /// <summary>Migrates in, equips the sword it starts with, and flags when the slot update returns.</summary>
    private sealed class Equipper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private bool _sent;

        public Equipper(int characterId) => _characterId = characterId;

        public TaskCompletionSource Equipped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChangeSlotPositionRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);       // timestamp
                w.WriteByte(1);      // EQUIP tab
                w.WriteShort(3);     // from inventory slot 3
                w.WriteShort(-11);   // to weapon slot
                w.WriteShort(1);     // quantity
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInvOp)
            {
                Equipped.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task EquipRequest_MovesItemToEquippedSlot()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Knight", MapId = 100000000 });
        hero.EquippedItems.Add(Equip(1302000, pos: 3, charId: hero.Id)); // a sword sitting in the bag
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Equipper(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Equipped.Task.WaitAsync(cts.Token);

        InventoryItem sword = Assert.Single(hero.EquippedItems);
        Assert.Equal((short)-11, sword.Position); // moved from bag slot 3 to the weapon slot
    }
}
