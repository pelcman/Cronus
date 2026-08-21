using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>One listing in a personal shop: the item template (its Quantity = units per bundle),
/// how many bundles remain, and the price per bundle (ports <c>MaplePlayerShopItem</c>).</summary>
public sealed class PlayerShopItem
{
    public PlayerShopItem(InventoryItem item, short bundles, int price)
    {
        Item = item;
        Bundles = bundles;
        Price = price;
    }

    public InventoryItem Item { get; }

    public short Bundles { get; set; }

    public int Price { get; }
}

/// <summary>
/// A personal shop (露店) set up in a Free Market map (ports <c>MaplePlayerShop</c>): the owner
/// lists items, up to three visitors browse and buy. Anchored to the owner with a balloon once
/// opened for business (MRP_Balloon).
/// </summary>
public sealed class PlayerShop
{
    /// <summary>The wire "game type" of a personal shop room (AbstractPlayerStore.getGameType).</summary>
    public const int GameType = 4;

    public const int MaxSize = 4; // owner + 3 visitors

    /// <summary>Close reasons (shopErrorMessage): the room closed / kicked / everything sold.</summary>
    public const byte CloseReasonClosed = 3;
    public const byte CloseReasonKicked = 5;
    public const byte CloseReasonSoldOut = 14;

    public PlayerShop(int objectId, FieldPlayer owner, string description, int itemId)
    {
        ObjectId = objectId;
        Owner = owner;
        Description = description;
        ItemId = itemId;
    }

    public int ObjectId { get; }

    public FieldPlayer Owner { get; }

    public string Description { get; }

    /// <summary>The store-permit item that opened the shop (its last digit picks the balloon icon).</summary>
    public int ItemId { get; }

    /// <summary>False while stocking; true once open for business (balloon shown).</summary>
    public bool Open { get; set; }

    /// <summary>Visitor seats 1..3 (index 0 = seat 1).</summary>
    public FieldPlayer?[] Visitors { get; } = new FieldPlayer?[MaxSize - 1];

    public List<PlayerShopItem> Items { get; } = new();

    public int Size => 1 + Visitors.Count(v => v is not null);

    /// <summary>The seat of a character: 0 owner, 1..3 visitor, -1 not here.</summary>
    public int SeatOf(int characterId)
    {
        if (Owner.Character.Id == characterId)
        {
            return 0;
        }

        for (int i = 0; i < Visitors.Length; i++)
        {
            if (Visitors[i]?.Character.Id == characterId)
            {
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>The first free visitor seat (1..3), or -1 when full.</summary>
    public int FreeSeat()
    {
        for (int i = 0; i < Visitors.Length; i++)
        {
            if (Visitors[i] is null)
            {
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>True when every listing has sold out (the shop then closes itself).</summary>
    public bool IsSoldOut => Items.Count > 0 && Items.All(i => i.Bundles <= 0);
}

/// <summary>Channel-wide index of personal shops, by balloon object id and participant. Object ids
/// are offset so they never collide with <see cref="MiniGameRegistry"/> ids (MRP_Enter carries only
/// the id).</summary>
public sealed class PlayerShopRegistry
{
    private const int ObjectIdBase = 1 << 20;

    private readonly ConcurrentDictionary<int, PlayerShop> _byObjectId = new();
    private readonly ConcurrentDictionary<int, PlayerShop> _byCharacter = new();
    private int _nextObjectId;

    public PlayerShop Create(FieldPlayer owner, string description, int itemId)
    {
        var shop = new PlayerShop(ObjectIdBase + Interlocked.Increment(ref _nextObjectId), owner, description, itemId);
        _byObjectId[shop.ObjectId] = shop;
        _byCharacter[owner.Character.Id] = shop;
        return shop;
    }

    public PlayerShop? Get(int objectId)
        => _byObjectId.TryGetValue(objectId, out PlayerShop? shop) ? shop : null;

    public PlayerShop? GetForCharacter(int characterId)
        => _byCharacter.TryGetValue(characterId, out PlayerShop? shop) ? shop : null;

    /// <summary>Open shops whose owner is standing in a map (for balloon replay on entry).</summary>
    public IReadOnlyList<PlayerShop> ShopsInMap(int mapId)
        => _byObjectId.Values.Where(s => s.Open && s.Owner.Character.MapId == mapId).ToList();

    public void SetVisitor(PlayerShop shop, int seat, FieldPlayer visitor)
    {
        shop.Visitors[seat - 1] = visitor;
        _byCharacter[visitor.Character.Id] = shop;
    }

    public void RemoveVisitor(PlayerShop shop, int seat)
    {
        if (shop.Visitors[seat - 1] is { } visitor)
        {
            _byCharacter.TryRemove(visitor.Character.Id, out _);
            shop.Visitors[seat - 1] = null;
        }
    }

    public void Remove(PlayerShop shop)
    {
        _byObjectId.TryRemove(shop.ObjectId, out _);
        _byCharacter.TryRemove(shop.Owner.Character.Id, out _);
        foreach (FieldPlayer? visitor in shop.Visitors)
        {
            if (visitor is not null)
            {
                _byCharacter.TryRemove(visitor.Character.Id, out _);
            }
        }
    }
}
