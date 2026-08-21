using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>One sale a hired merchant made while the owner was away (ports <c>BoughtItem</c>).</summary>
public sealed record SoldRecord(int ItemId, short Quantity, int TotalPrice, string Buyer);

/// <summary>
/// A hired merchant (雇用商人) — an employee NPC that keeps selling on the Free Market map while
/// its owner is elsewhere or offline (ports <c>HiredMerchant</c>). Reuses
/// <see cref="PlayerShopItem"/> listings. Held in memory for now — a server restart closes all
/// merchants (persistence is a follow-up).
/// </summary>
public sealed class HiredMerchant
{
    /// <summary>The wire "game type" of an entrusted shop (AbstractPlayerStore.getGameType).</summary>
    public const int GameType = 5;

    public const int MaxVisitors = 3;

    public HiredMerchant(int objectId, Character owner, string description, int itemId, int mapId, short x, short y, int foothold)
    {
        ObjectId = objectId;
        OwnerId = owner.Id;
        OwnerName = owner.Name;
        Description = description;
        ItemId = itemId;
        MapId = mapId;
        X = x;
        Y = y;
        Foothold = foothold;
        StartedAtTick = Environment.TickCount64;
    }

    /// <summary>Restores a merchant from its persisted snapshot (a server restart).</summary>
    public HiredMerchant(int objectId, HiredMerchantData data)
    {
        ObjectId = objectId;
        OwnerId = data.OwnerId;
        OwnerName = data.OwnerName;
        Description = data.Description;
        ItemId = data.ItemId;
        MapId = data.MapId;
        X = data.X;
        Y = data.Y;
        Foothold = data.Foothold;
        Meso = data.Meso;
        StartedAtTick = Environment.TickCount64;
        Open = true; // restored stores go straight back to selling
        foreach (MerchantListing listing in data.Listings)
        {
            Items.Add(new PlayerShopItem(listing.Item, listing.Bundles, listing.Price));
        }

        foreach (MerchantSale sale in data.Sales)
        {
            Sold.Add(new SoldRecord(sale.ItemId, sale.Quantity, sale.TotalPrice, sale.Buyer));
        }
    }

    /// <summary>The persistable snapshot of this merchant.</summary>
    public HiredMerchantData Snapshot() => new()
    {
        OwnerId = OwnerId,
        OwnerName = OwnerName,
        Description = Description,
        ItemId = ItemId,
        MapId = MapId,
        X = X,
        Y = Y,
        Foothold = Foothold,
        Meso = Meso,
        Listings = Items.Select(i => new MerchantListing(i.Item, i.Bundles, i.Price)).ToList(),
        Sales = Sold.Select(s => new MerchantSale(s.ItemId, s.Quantity, s.TotalPrice, s.Buyer)).ToList(),
    };

    public int ObjectId { get; }

    public int OwnerId { get; }

    public string OwnerName { get; }

    public string Description { get; }

    /// <summary>The employee permit item (503xxxx); its tail digits pick the NPC look.</summary>
    public int ItemId { get; }

    public int MapId { get; }

    public short X { get; }

    public short Y { get; }

    public int Foothold { get; }

    public long StartedAtTick { get; }

    /// <summary>False while the owner is stocking/managing; true while selling on the map.</summary>
    public bool Open { get; set; }

    /// <summary>Meso earned so far (already taxed).</summary>
    public int Meso { get; set; }

    public List<PlayerShopItem> Items { get; } = new();

    public List<SoldRecord> Sold { get; } = new();

    /// <summary>Browsing visitors, seats 1..3 (index 0 = seat 1).</summary>
    public FieldPlayer?[] Visitors { get; } = new FieldPlayer?[MaxVisitors];

    /// <summary>The owner's session while managing the store, or null.</summary>
    public FieldPlayer? Manager { get; set; }

    public int Size => 1 + Visitors.Count(v => v is not null);

    /// <summary>Elapsed open time in seconds (the room UI's clock, ports getTimeLeft).</summary>
    public int UpTimeSeconds => (int)((Environment.TickCount64 - StartedAtTick) / 1000);

    /// <summary>0 for the managing owner, 1..3 for visitors, -1 when not inside.</summary>
    public int SeatOf(int characterId)
    {
        if (Manager?.Character.Id == characterId)
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

    /// <summary>The entrusted-store sales tax (ports <c>GameConstants.EntrustedStoreTax</c>).</summary>
    public static int Tax(int meso) => meso switch
    {
        >= 100_000_000 => (int)Math.Round(0.03 * meso),
        >= 25_000_000 => (int)Math.Round(0.025 * meso),
        >= 10_000_000 => (int)Math.Round(0.02 * meso),
        >= 5_000_000 => (int)Math.Round(0.015 * meso),
        >= 1_000_000 => (int)Math.Round(0.009 * meso),
        >= 100_000 => (int)Math.Round(0.004 * meso),
        _ => 0,
    };
}

/// <summary>Channel-wide index of hired merchants: by mini-room id, owner, participant, and map.
/// Object ids sit in their own range so MRP_Enter ids never collide with games/shops.</summary>
public sealed class HiredMerchantRegistry
{
    private const int ObjectIdBase = 2 << 20;

    private readonly ConcurrentDictionary<int, HiredMerchant> _byObjectId = new();
    private readonly ConcurrentDictionary<int, HiredMerchant> _byOwner = new();
    private readonly ConcurrentDictionary<int, HiredMerchant> _byParticipant = new();
    private readonly IHiredMerchantRepository? _repo;
    private int _nextObjectId;

    public HiredMerchantRegistry(IHiredMerchantRepository? repo = null)
    {
        _repo = repo;
        if (repo is null)
        {
            return;
        }

        // Stores that were open when the server went down come straight back up.
        foreach (HiredMerchantData data in repo.LoadAll())
        {
            var merchant = new HiredMerchant(ObjectIdBase + Interlocked.Increment(ref _nextObjectId), data);
            _byObjectId[merchant.ObjectId] = merchant;
            _byOwner[merchant.OwnerId] = merchant;
        }
    }

    public HiredMerchant Create(Character owner, string description, int itemId, int mapId, short x, short y, int foothold)
    {
        var merchant = new HiredMerchant(
            ObjectIdBase + Interlocked.Increment(ref _nextObjectId), owner, description, itemId, mapId, x, y, foothold);
        _byObjectId[merchant.ObjectId] = merchant;
        _byOwner[owner.Id] = merchant;
        Persist(merchant);
        return merchant;
    }

    /// <summary>Flushes a merchant's state (stock, banked meso, sales) to the store, if any.</summary>
    public void Persist(HiredMerchant merchant) => _repo?.Save(merchant.Snapshot());

    public HiredMerchant? Get(int objectId)
        => _byObjectId.TryGetValue(objectId, out HiredMerchant? m) ? m : null;

    public HiredMerchant? GetByOwner(int ownerId)
        => _byOwner.TryGetValue(ownerId, out HiredMerchant? m) ? m : null;

    /// <summary>The merchant a character is currently inside (managing or browsing), or null.</summary>
    public HiredMerchant? GetForParticipant(int characterId)
        => _byParticipant.TryGetValue(characterId, out HiredMerchant? m) ? m : null;

    /// <summary>Merchants standing in a map (open or in maintenance — the NPC stays visible).</summary>
    public IReadOnlyList<HiredMerchant> MerchantsInMap(int mapId)
        => _byObjectId.Values.Where(m => m.Open && m.MapId == mapId).ToList();

    public void SetVisitor(HiredMerchant merchant, int seat, FieldPlayer visitor)
    {
        merchant.Visitors[seat - 1] = visitor;
        _byParticipant[visitor.Character.Id] = merchant;
    }

    public void RemoveVisitor(HiredMerchant merchant, int seat)
    {
        if (merchant.Visitors[seat - 1] is { } visitor)
        {
            _byParticipant.TryRemove(visitor.Character.Id, out _);
            merchant.Visitors[seat - 1] = null;
        }
    }

    public void SetManager(HiredMerchant merchant, FieldPlayer owner)
    {
        merchant.Manager = owner;
        _byParticipant[owner.Character.Id] = merchant;
    }

    public void RemoveManager(HiredMerchant merchant)
    {
        if (merchant.Manager is { } manager)
        {
            _byParticipant.TryRemove(manager.Character.Id, out _);
            merchant.Manager = null;
        }
    }

    public void Remove(HiredMerchant merchant)
    {
        _byObjectId.TryRemove(merchant.ObjectId, out _);
        _byOwner.TryRemove(merchant.OwnerId, out _);
        RemoveManager(merchant);
        for (int seat = 1; seat <= HiredMerchant.MaxVisitors; seat++)
        {
            RemoveVisitor(merchant, seat);
        }

        _repo?.Delete(merchant.OwnerId);
    }
}
