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

    /// <summary>Migrates in, drops its potion stack to the ground (dst==0), flags on the slot update.</summary>
    private sealed class Dropper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private bool _sent;

        public Dropper(int characterId) => _characterId = characterId;

        public TaskCompletionSource Dropped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                w.WriteInt(0);     // timestamp
                w.WriteByte(2);    // USE tab
                w.WriteShort(1);   // from slot 1
                w.WriteShort(0);   // dst 0 = drop to ground
                w.WriteShort(5);   // whole stack
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInvOp)
            {
                Dropped.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task DropToGround_RemovesFromInventoryAndSpawnsFieldDrop()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Giver", MapId = 100000000 });
        hero.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 5, CharacterId = hero.Id });
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Dropper(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Dropped.Task.WaitAsync(cts.Token);

        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 2000000); // left the inventory
        FieldDrop drop = Assert.Single(fields.Get(100000000).Drops);
        Assert.Equal(2000000, drop.ItemId);
        Assert.Equal((short)5, drop.Quantity);
        Assert.True(drop.IsPlayerDrop);
    }

    /// <summary>Drops its equip to the ground, picks it straight back up, flags on the second slot update.</summary>
    private sealed class EquipDropper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private bool _sent;
        private int _invOps;

        public EquipDropper(int characterId) => _characterId = characterId;

        public TaskCompletionSource RoundTripped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                w.WriteInt(0);
                w.WriteByte(1);    // EQUIP tab
                w.WriteShort(3);   // from slot 3
                w.WriteShort(0);   // dst 0 = drop to ground
                w.WriteShort(1);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opDropEnter)
            {
                p.ReadByte();
                int dropOid = p.ReadInt();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.DropPickUpRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteShort(0);
                w.WriteShort(0);
                w.WriteInt(dropOid);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInvOp && ++_invOps == 2) // 1 = the drop's remove, 2 = the pickup's add
            {
                RoundTripped.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task DroppedEquip_KeepsItsStatsThroughPickup()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Lender", MapId = 100000000 });
        var sword = Equip(1302000, pos: 3, charId: hero.Id);
        sword.Watk = 17;
        sword.UpgradeSlots = 5; // scrolled twice — instance state that must survive
        hero.EquippedItems.Add(sword);
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new EquipDropper(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.RoundTripped.Task.WaitAsync(cts.Token);

        InventoryItem back = Assert.Single(hero.EquippedItems, i => i.ItemId == 1302000);
        Assert.Equal((short)17, back.Watk);        // the same instance came back
        Assert.Equal((byte)5, back.UpgradeSlots);  // scroll state intact
        Assert.Empty(fields.Get(100000000).Drops);
    }

    /// <summary>Migrates in, spawns a sword via /item, flags on the slot update.</summary>
    private sealed class Requester : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _command;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private bool _sent;

        public Requester(int characterId, string command)
        {
            _characterId = characterId;
            _command = command;
        }

        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString(_command);
                w.WriteByte(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opInvOp)
            {
                Done.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task CreatedEquip_GetsWzBaseStats()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Smith", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // The item provider knows the sword's base stats (as the wz would supply).
        var items = new InMemoryItemProvider(
            Array.Empty<ConsumeSpec>(),
            equips: new Dictionary<int, EquipStats> { [1302000] = new EquipStats { Watk = 17, UpgradeSlots = 7 } });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Requester(hero.Id, "/item 1302000");
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, items: items);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Done.Task.WaitAsync(cts.Token);

        InventoryItem sword = Assert.Single(hero.EquippedItems, i => i.ItemId == 1302000);
        Assert.Equal((short)17, sword.Watk);     // wz base attack
        Assert.Equal((byte)7, sword.UpgradeSlots);
    }
}
