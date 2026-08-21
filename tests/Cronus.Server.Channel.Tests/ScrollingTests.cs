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

public class ScrollingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static InventoryItem Sword(byte slots = 7) => new()
    {
        ItemId = 1302000, Position = -11, Quantity = 1, Watk = 17, UpgradeSlots = slots,
    };

    [Fact]
    public void Success_AppliesStats_BurnsSlot_RaisesLevel()
    {
        InventoryItem sword = Sword();
        var spec = new ScrollSpec { Success = 100, Stats = new EquipStats { Watk = 2, Acc = 1 } };

        ScrollResult result = Scrolling.Apply(sword, 2043000, spec, equipTuc: 7, whiteScroll: false, new Random(1));

        Assert.Equal(ScrollResult.Success, result);
        Assert.Equal(19, sword.Watk);
        Assert.Equal(1, sword.Acc);
        Assert.Equal(6, sword.UpgradeSlots);
        Assert.Equal(1, sword.Level);
    }

    [Fact]
    public void Fail_BurnsSlot_UnlessWhiteScrolled()
    {
        var spec = new ScrollSpec { Success = -1, Cursed = 0 }; // -1: rng.Next(100) <= -1 never

        InventoryItem unprotected = Sword();
        Assert.Equal(ScrollResult.Fail, Scrolling.Apply(unprotected, 2043000, spec, 7, whiteScroll: false, new Random(1)));
        Assert.Equal(6, unprotected.UpgradeSlots);
        Assert.Equal(17, unprotected.Watk); // stats untouched
        Assert.Equal(0, unprotected.Level);

        InventoryItem guarded = Sword();
        Assert.Equal(ScrollResult.Fail, Scrolling.Apply(guarded, 2043000, spec, 7, whiteScroll: true, new Random(1)));
        Assert.Equal(7, guarded.UpgradeSlots); // protected
    }

    [Fact]
    public void Curse_OnFailedRoll_DestroysEquip()
    {
        var spec = new ScrollSpec { Success = -1, Cursed = 100 };
        Assert.Equal(ScrollResult.Curse, Scrolling.Apply(Sword(), 2043000, spec, 7, false, new Random(1)));
    }

    [Fact]
    public void CleanSlate_RestoresSlots_OnlyBelowBaseCount()
    {
        var spec = new ScrollSpec { Success = 100 };

        InventoryItem used = Sword(slots: 3); // level 0, 3 of base 7 remain -> restorable
        Assert.Equal(ScrollResult.Success, Scrolling.Apply(used, 2049000, spec, 7, false, new Random(1)));
        Assert.Equal(4, used.UpgradeSlots);
        Assert.Equal(0, used.Level); // clean slates never level

        InventoryItem fresh = Sword(slots: 7); // nothing consumed -> nothing to restore
        Assert.Equal(ScrollResult.Fail, Scrolling.Apply(fresh, 2049000, spec, 7, false, new Random(1)));
        Assert.Equal(7, fresh.UpgradeSlots);

        InventoryItem plus2 = Sword(slots: 3);
        Scrolling.Apply(plus2, 2049006, spec, 7, false, new Random(1));
        Assert.Equal(5, plus2.UpgradeSlots); // 2049006+ restore two
    }

    [Fact]
    public void Chaos_OnlyDriftsNonzeroStats()
    {
        InventoryItem sword = Sword();
        var spec = new ScrollSpec { Success = 100 };

        Scrolling.Apply(sword, 2049100, spec, 7, false, new Random(7));

        Assert.Equal(0, sword.Str); // zero stats stay zero
        Assert.InRange(sword.Watk, 17 - 4, 17 + 4);
        Assert.Equal(6, sword.UpgradeSlots);
        Assert.Equal(1, sword.Level);
    }

    [Theory]
    [InlineData(2043000, 1302000, true)]   // 1h-sword scroll on a 1h sword
    [InlineData(2043000, 1402000, false)]  // ...but not on a 2h sword
    [InlineData(2044500, 1452000, true)]   // bow scroll on a bow
    [InlineData(2040000, 1002000, true)]   // helmet scroll on a cap
    public void CanScroll_MatchesEquipFamily(int scrollId, int equipId, bool expected)
        => Assert.Equal(expected, Scrolling.CanScroll(scrollId, equipId));

    // ---- e2e: worn sword + 100% ATT scroll -> success flash + updated equip ----

    private sealed class Scroller : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEffect = ServerOps.Get(ServerOpcode.UserItemUpgradeEffect);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private bool _sent;

        public Scroller(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(bool Success, bool Cursed)> Flash { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserUpgradeItemUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);      // update time
                w.WriteShort(1);    // scroll in USE slot 1
                w.WriteShort(-11);  // the worn weapon
                w.WriteShort(0);    // no white scroll
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opEffect)
            {
                p.ReadInt(); // character id
                Flash.TrySetResult((p.ReadByte() != 0, p.ReadByte() != 0));
            }
            else if (opcode == _opInvOp && _sent)
            {
                InventoryUpdated.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task ScrollingWornEquip_Succeeds_AndUpdatesItem()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });
        InventoryItem sword = Sword();
        sword.CharacterId = hero.Id;
        hero.EquippedItems.Add(sword);
        hero.EquippedItems.Add(new InventoryItem { ItemId = 2043000, Position = 1, Quantity = 3, CharacterId = hero.Id });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var items = new InMemoryItemProvider(
            Array.Empty<ConsumeSpec>(),
            scrolls: new Dictionary<int, ScrollSpec>
            {
                [2043000] = new ScrollSpec { Success = 100, Stats = new EquipStats { Watk = 2 } },
            });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Scroller(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, items: items);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (bool success, bool cursed) = await client.Flash.Task.WaitAsync(cts.Token);
        await client.InventoryUpdated.Task.WaitAsync(cts.Token);

        Assert.True(success);
        Assert.False(cursed);
        Assert.Equal(19, sword.Watk);
        Assert.Equal(6, sword.UpgradeSlots);
        Assert.Equal(1, sword.Level);
        Assert.Equal(2, hero.EquippedItems.Single(i => i.ItemId == 2043000).Quantity); // one scroll used
    }
}
