using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>One participant's side of a trade: staged items/meso and the lock state.</summary>
public sealed class TradeSide
{
    public TradeSide(FieldPlayer player, byte slot)
    {
        Player = player;
        Slot = slot;
    }

    public FieldPlayer Player { get; }

    /// <summary>The absolute room slot: 0 = the starter, 1 = the invited visitor.</summary>
    public byte Slot { get; }

    /// <summary>Items staged for the exchange (already removed from the owner's inventory).</summary>
    public List<InventoryItem> Items { get; } = new();

    /// <summary>Meso staged for the exchange (already deducted from the owner).</summary>
    public int Meso { get; set; }

    /// <summary>True once this side pressed Trade (no further changes allowed).</summary>
    public bool Locked { get; set; }
}

/// <summary>
/// A two-player trade room (ports <c>MapleTrade</c>): the starter creates it, invites a partner who
/// enters, both stage items/meso, and the exchange happens when both lock.
/// </summary>
public sealed class Trade
{
    public Trade(FieldPlayer starter) => Starter = new TradeSide(starter, 0);

    public TradeSide Starter { get; }

    public TradeSide? Visitor { get; private set; }

    /// <summary>Character id the starter invited (set on invite, before the visitor enters).</summary>
    public int InvitedCharacterId { get; set; }

    /// <summary>True once the visitor has entered the room.</summary>
    public bool VisitorEntered { get; set; }

    public TradeSide Join(FieldPlayer visitor)
    {
        Visitor = new TradeSide(visitor, 1);
        return Visitor;
    }

    /// <summary>The side belonging to a character, or null.</summary>
    public TradeSide? SideOf(int characterId)
        => Starter.Player.Character.Id == characterId ? Starter
            : Visitor?.Player.Character.Id == characterId ? Visitor
            : null;

    /// <summary>The other participant's side, or null before the visitor joins.</summary>
    public TradeSide? PartnerOf(TradeSide side) => side == Starter ? Visitor : Starter;

    public bool BothLocked => Starter.Locked && Visitor is { Locked: true };

    private int _closed;

    /// <summary>
    /// Claims the one-and-only close of this trade (complete or cancel). Both participants'
    /// sessions can race to finish/cancel simultaneously; only the first claimer proceeds.
    /// </summary>
    public bool TryClose() => Interlocked.Exchange(ref _closed, 1) == 0;
}

/// <summary>Active trades by participant character id (both participants map to the same trade).</summary>
public sealed class TradeRegistry
{
    private readonly ConcurrentDictionary<int, Trade> _byCharacter = new();

    public Trade? Get(int characterId) => _byCharacter.TryGetValue(characterId, out Trade? t) ? t : null;

    public bool TryAdd(int characterId, Trade trade) => _byCharacter.TryAdd(characterId, trade);

    public void Remove(Trade trade)
    {
        _byCharacter.TryRemove(trade.Starter.Player.Character.Id, out _);
        if (trade.Visitor is { } visitor)
        {
            _byCharacter.TryRemove(visitor.Player.Character.Id, out _);
        }
        else if (trade.InvitedCharacterId != 0)
        {
            _byCharacter.TryRemove(trade.InvitedCharacterId, out _);
        }
    }
}
