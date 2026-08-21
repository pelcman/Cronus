using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// An Omok (五目並べ) or match-card (神経衰弱) game room (ports <c>MapleMiniGame</c>): a two-seat
/// mini-room anchored to its owner on the field with a balloon, playable once the visitor readies
/// up. Pure game state + rules live here; the channel handler drives packets around it.
/// </summary>
public sealed class MiniGame
{
    public const int TypeOmok = 1;
    public const int TypeMatchCard = 2;

    /// <summary>Result codes for the game-result packet: 0 lose (gave up), 1 tie, 2 win.</summary>
    public const int ResultLose = 0;
    public const int ResultTie = 1;
    public const int ResultWin = 2;

    /// <summary>The quest ids whose custom data records "losses,ties,wins" (the reference's store).</summary>
    public const int OmokRecordQuest = 122200;
    public const int MatchCardRecordQuest = 122210;

    public MiniGame(int objectId, int gameType, FieldPlayer owner, string description, string password, int pieceType)
    {
        ObjectId = objectId;
        GameType = gameType;
        Owner = owner;
        Description = description;
        Password = password;
        PieceType = pieceType;
        ItemId = gameType == TypeOmok ? 4080000 + pieceType : 4080100;
    }

    public int ObjectId { get; }

    /// <summary>1 = Omok, 2 = match card (the wire "game type" of the room).</summary>
    public int GameType { get; }

    public FieldPlayer Owner { get; }

    /// <summary>The single visitor seat (slot 1), or null.</summary>
    public FieldPlayer? Visitor { get; set; }

    public string Description { get; }

    public string Password { get; }

    /// <summary>Omok: the stone set (item 4080000+n). Match card: board size 0/1/2 → 12/20/30 cards.</summary>
    public int PieceType { get; }

    public int ItemId { get; }

    /// <summary>True while in the lobby (no round running).</summary>
    public bool Open { get; set; } = true;

    /// <summary>The visitor's ready state (index by seat; only seat 1 is used).</summary>
    public bool[] Ready { get; } = new bool[2];

    /// <summary>Per seat: leave once the current round ends.</summary>
    public bool[] ExitAfter { get; } = new bool[2];

    /// <summary>Match-card points this round, per seat.</summary>
    public int[] Points { get; } = new int[2];

    /// <summary>Which seat starts the next round (the previous round's loser).</summary>
    public int Loser { get; set; }

    /// <summary>Seat that asked for a tie, or -1.</summary>
    public int RequestedTie { get; set; } = -1;

    /// <summary>Match card: 1 = first card of the pair is being picked, 0 = second.</summary>
    public int Turn { get; set; } = 1;

    /// <summary>Match card: the first flipped card of the current pair.</summary>
    public int FirstSlot { get; set; }

    /// <summary>Omok board, 15×15; 0 empty, else the piece type placed.</summary>
    public int[,] Board { get; private set; } = new int[15, 15];

    /// <summary>Match-card layout: card id per board slot (1-based on the wire).</summary>
    public List<int> Cards { get; } = new();

    /// <summary>Pairs needed to clear the match-card board (6/10/15 by board size).</summary>
    public int MatchesToWin => PieceType == 0 ? 6 : PieceType == 1 ? 10 : 15;

    /// <summary>Cards on the match-card board (12/20/30 by board size).</summary>
    public int CardCount => PieceType == 1 ? 20 : PieceType == 2 ? 30 : 12;

    /// <summary>Current occupancy (owner + visitor).</summary>
    public int Size => Visitor is null ? 1 : 2;

    public const int MaxSize = 2;

    /// <summary>The seat of a character in this room: 0 owner, 1 visitor, -1 not here.</summary>
    public int SeatOf(int characterId)
        => Owner.Character.Id == characterId ? 0
            : Visitor?.Character.Id == characterId ? 1
            : -1;

    public FieldPlayer? PlayerAt(int seat) => seat == 0 ? Owner : Visitor;

    /// <summary>Sets up a fresh round: clears the Omok board / shuffles the match cards.</summary>
    public void StartRound()
    {
        if (GameType == TypeMatchCard)
        {
            Cards.Clear();
            for (int i = 0; i < MatchesToWin; i++)
            {
                Cards.Add(i);
                Cards.Add(i);
            }

            for (int i = Cards.Count - 1; i > 0; i--) // Fisher–Yates (ports Collections.shuffle)
            {
                int j = Random.Shared.Next(i + 1);
                (Cards[i], Cards[j]) = (Cards[j], Cards[i]);
            }
        }
        else
        {
            Board = new int[15, 15];
        }

        Points[0] = 0;
        Points[1] = 0;
        Turn = 1;
        FirstSlot = 0;
        Open = false;
    }

    /// <summary>The card id at a 1-based wire slot.</summary>
    public int CardId(int slot) => Cards[slot - 1];

    /// <summary>Places an Omok stone if the square is free; false when occupied.</summary>
    public bool TryPlacePiece(int x, int y, int type)
    {
        if (x is < 0 or >= 15 || y is < 0 or >= 15 || Board[x, y] != 0)
        {
            return false;
        }

        Board[x, y] = type;
        return true;
    }

    /// <summary>True if any five-in-a-row of <paramref name="type"/> exists (ports searchCombo).</summary>
    public bool HasFiveInARow(int type)
    {
        for (int y = 0; y < 15; y++)
        {
            for (int x = 0; x < 15; x++)
            {
                if (RunFrom(x, y, 1, 0, type) || RunFrom(x, y, 0, 1, type)
                    || RunFrom(x, y, 1, 1, type) || RunFrom(x, y, -1, 1, type))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool RunFrom(int x, int y, int dx, int dy, int type)
    {
        for (int i = 0; i < 5; i++)
        {
            int px = x + dx * i;
            int py = y + dy * i;
            if (px is < 0 or >= 15 || py is < 0 or >= 15 || Board[px, py] != type)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Advances whose turn it is / who starts next round (ports nextLoser).</summary>
    public void NextLoser() => Loser = Loser >= MaxSize - 1 ? 0 : Loser + 1;

    // ---- win/loss records, stored as "losses,ties,wins" quest custom data like the reference ----

    private int RecordQuest => GameType == TypeOmok ? OmokRecordQuest : MatchCardRecordQuest;

    private int[] RecordOf(Character c)
    {
        if (c.StartedQuests.TryGetValue(RecordQuest, out string? data) && data is not null)
        {
            string[] parts = data.Split(',');
            if (parts.Length == 3
                && int.TryParse(parts[0], out int l) && int.TryParse(parts[1], out int t) && int.TryParse(parts[2], out int w))
            {
                return new[] { l, t, w };
            }
        }

        return new[] { 0, 0, 0 };
    }

    public int Losses(Character c) => RecordOf(c)[0];

    public int Ties(Character c) => RecordOf(c)[1];

    public int Wins(Character c) => RecordOf(c)[2];

    /// <summary>The ranking score shown in the room (ports getScore's simple formula).</summary>
    public int Score(Character c)
    {
        int[] r = RecordOf(c);
        return 2000 + r[2] * 2 + r[1] - r[0] * 2;
    }

    /// <summary>Bumps one seat's record: result 0 = loss, 1 = tie, 2 = win.</summary>
    public void AddResult(int seat, int result)
    {
        Character? c = PlayerAt(seat)?.Character;
        if (c is null)
        {
            return;
        }

        int[] r = RecordOf(c);
        r[result]++;
        c.StartedQuests[RecordQuest] = $"{r[0]},{r[1]},{r[2]}";
    }
}

/// <summary>Channel-wide index of open game rooms, by balloon object id and participant.</summary>
public sealed class MiniGameRegistry
{
    private readonly ConcurrentDictionary<int, MiniGame> _byObjectId = new();
    private readonly ConcurrentDictionary<int, MiniGame> _byCharacter = new();
    private int _nextObjectId;

    public MiniGame Create(int gameType, FieldPlayer owner, string description, string password, int pieceType)
    {
        var game = new MiniGame(Interlocked.Increment(ref _nextObjectId), gameType, owner, description, password, pieceType);
        _byObjectId[game.ObjectId] = game;
        _byCharacter[owner.Character.Id] = game;
        return game;
    }

    public MiniGame? Get(int objectId)
        => _byObjectId.TryGetValue(objectId, out MiniGame? game) ? game : null;

    public MiniGame? GetForCharacter(int characterId)
        => _byCharacter.TryGetValue(characterId, out MiniGame? game) ? game : null;

    /// <summary>All rooms whose owner is standing in a map (for balloon replay on entry).</summary>
    public IReadOnlyList<MiniGame> GamesInMap(int mapId)
        => _byObjectId.Values.Where(g => g.Owner.Character.MapId == mapId).ToList();

    public void SetVisitor(MiniGame game, FieldPlayer visitor)
    {
        game.Visitor = visitor;
        _byCharacter[visitor.Character.Id] = game;
    }

    public void RemoveVisitor(MiniGame game)
    {
        if (game.Visitor is { } visitor)
        {
            _byCharacter.TryRemove(visitor.Character.Id, out _);
            game.Visitor = null;
        }
    }

    public void Remove(MiniGame game)
    {
        _byObjectId.TryRemove(game.ObjectId, out _);
        _byCharacter.TryRemove(game.Owner.Character.Id, out _);
        if (game.Visitor is { } visitor)
        {
            _byCharacter.TryRemove(visitor.Character.Id, out _);
        }
    }
}
