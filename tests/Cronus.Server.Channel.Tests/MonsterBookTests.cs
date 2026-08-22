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

/// <summary>
/// Monster Book: picking up a card (238xxxx) registers it in the book — never the inventory —
/// with LP_MonsterBookSetCard + the card-get message; and a mob whose card the killer already
/// registered stops dropping that card.
/// </summary>
public class MonsterBookTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    /// <summary>Picks up the first drop and records the card-registration packets.</summary>
    private sealed class CardPicker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private readonly int _opSetCard = ServerOps.Get(ServerOpcode.MonsterBookSetCard);
        private readonly int _opInvOp = ServerOps.Get(ServerOpcode.InventoryOperation);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);

        public CardPicker(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(bool Added, int CardId, int Count)> CardSet { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> CardMessage { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Op counts of every InventoryOperation seen (the unlock form has zero ops).</summary>
        public List<int> InventoryOperationOpCounts { get; } = new();

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
            if (opcode == _opDropEnter)
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
            else if (opcode == _opSetCard)
            {
                bool added = p.ReadBool();
                int id = added ? p.ReadInt() : 0;
                int count = added ? p.ReadInt() : 0;
                CardSet.TrySetResult((added, id, count));
            }
            else if (opcode == _opMessage)
            {
                if (p.ReadByte() == 0 && p.ReadByte() == 2) // DropPickUp / PICKUP_MONSTER_CARD
                {
                    CardMessage.TrySetResult(p.ReadInt());
                }
            }
            else if (opcode == _opInvOp)
            {
                p.ReadByte();                             // unlock flag
                InventoryOperationOpCounts.Add(p.ReadByte()); // op count (0 = pure unlock)
            }
        }
    }

    [Fact]
    public async Task PickingUpACard_RegistersInTheBook_NotTheInventory()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Collector", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        fields.Get(100000000).AddItemDrop(2380000, quantity: 1, x: 15, y: 25, source: null);

        using var cts = new CancellationTokenSource(Timeout);
        var client = new CardPicker(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (bool added, int cardId, int count) = await client.CardSet.Task.WaitAsync(cts.Token);
        int messageCard = await client.CardMessage.Task.WaitAsync(cts.Token);

        Assert.True(added);
        Assert.Equal(2380000, cardId);
        Assert.Equal(1, count);
        Assert.Equal(2380000, messageCard);
        Assert.Equal(1, hero.MonsterCards[2380000]);                       // registered server-side
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 2380000); // never itemized
        Assert.All(client.InventoryOperationOpCounts, n => Assert.Equal(0, n)); // only the unlock form
        Assert.Contains(0, client.InventoryOperationOpCounts);                  // and it WAS sent (updateInv)
        Assert.Empty(fields.Get(100000000).Drops);
    }

    [Fact]
    public async Task AFullyRegisteredCard_PicksUpAsTheNoOpForm()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Maxed", MapId = 100000000 });
        hero.MonsterCards[2380000] = 5; // book already full for this card
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        fields.Get(100000000).AddItemDrop(2380000, quantity: 1, x: 15, y: 25, source: null);

        using var cts = new CancellationTokenSource(Timeout);
        var client = new CardPicker(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (bool added, _, _) = await client.CardSet.Task.WaitAsync(cts.Token);

        Assert.False(added);                       // the "already full" form
        Assert.Equal(5, hero.MonsterCards[2380000]);
        Assert.Empty(fields.Get(100000000).Drops); // the drop is still consumed
    }

    /// <summary>Kills the spawned mob once and reports every item id that drops.</summary>
    private sealed class CardHunter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opMobLeave = ServerOps.Get(ServerOpcode.MobLeaveField);
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private bool _setField;
        private int _mobOid = -1;
        private bool _attacked;
        private readonly List<int> _dropIds = new();

        public CardHunter(int characterId) => _characterId = characterId;

        public TaskCompletionSource<List<int>> DropsAfterKill { get; } =
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

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                _setField = true;
                await MaybeAttack(session);
            }
            else if (opcode == _opMobEnter)
            {
                _mobOid = p.ReadInt();
                await MaybeAttack(session);
            }
            else if (opcode == _opDropEnter)
            {
                p.ReadByte();               // enter type
                p.ReadInt();                // drop oid
                if (p.ReadByte() == 0)      // 0 = item (1 = meso)
                {
                    _dropIds.Add(p.ReadInt());
                }
            }
            else if (opcode == _opMobLeave)
            {
                // Drops are spawned AFTER the mob-leave broadcast; give them a beat to arrive
                // before reporting what fell (covers the no-drop case deterministically too).
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    DropsAfterKill.TrySetResult(_dropIds);
                });
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (!_setField || _mobOid < 0 || _attacked)
            {
                return;
            }

            _attacked = true;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteByte(0);
            w.WriteInt(0); w.WriteInt(0);
            w.WriteByte(0x11);                 // 1 target, 1 hit
            w.WriteInt(0); w.WriteInt(0);
            w.WriteInt(0);                     // no skill
            w.WriteInt(0); w.WriteInt(0); w.WriteInt(0);
            w.WriteByte(0);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteByte(0);
            w.WriteInt(0);
            w.WriteInt(0);
            w.WriteInt(_mobOid);
            w.WriteBytes(new byte[4]);
            w.WriteBytes(new byte[8]);
            w.WriteShort(0);
            w.WriteInt(99);                    // lethal (mob has 50 HP)
            w.WriteInt(0);                     // mob crc
            await session.SendAsync(w.ToArray());
        }
    }

    private static (FieldRegistry Fields, InMemoryDropProvider Drops, InMemoryCharacterRepository Repo, Character Hero) CardMobWorld()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hunter", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 5 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);
        var drops = new InMemoryDropProvider(new Dictionary<int, IReadOnlyList<DropEntry>>
        {
            [100100] = new[] { new DropEntry(2380000, 1, 1, 0, 1000) }, // its card, always
        });
        return (fields, drops, repo, hero);
    }

    [Fact]
    public async Task AMobWhoseCardIsRegistered_StopsDroppingTheCard()
    {
        (FieldRegistry fields, InMemoryDropProvider drops, InMemoryCharacterRepository repo, Character hero) = CardMobWorld();
        hero.MonsterCards[2380000] = 1; // registered once — per the server rule, no more card drops
        repo.Save(hero);

        using var cts = new CancellationTokenSource(Timeout);
        var client = new CardHunter(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, drops: drops);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        List<int> dropped = await client.DropsAfterKill.Task.WaitAsync(cts.Token);

        Assert.DoesNotContain(2380000, dropped);
    }

    [Fact]
    public async Task AnUnregisteredCard_StillDrops()
    {
        (FieldRegistry fields, InMemoryDropProvider drops, InMemoryCharacterRepository repo, Character hero) = CardMobWorld();

        using var cts = new CancellationTokenSource(Timeout);
        var client = new CardHunter(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, drops: drops);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        List<int> dropped = await client.DropsAfterKill.Task.WaitAsync(cts.Token);

        Assert.Contains(2380000, dropped);
    }
}
