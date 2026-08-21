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

public class MiniGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static MiniGame NewOmok(FieldPlayer owner, int piece = 0)
        => new(objectId: 1, MiniGame.TypeOmok, owner, "room", "", piece);

    private static FieldPlayer Player(int id, string name)
        => new(new Character { Id = id, Name = name, MapId = 100000000 }, session: null!);

    // ---- rules ----

    [Theory]
    [InlineData(1, 0)]  // horizontal
    [InlineData(0, 1)]  // vertical
    [InlineData(1, 1)]  // diagonal
    [InlineData(-1, 1)] // anti-diagonal
    public void Omok_FiveInARow_Wins(int dx, int dy)
    {
        MiniGame game = NewOmok(Player(1, "A"));
        game.StartRound();
        int x0 = dx < 0 ? 7 : 3;
        for (int i = 0; i < 5; i++)
        {
            Assert.True(game.TryPlacePiece(x0 + dx * i, 3 + dy * i, type: 1));
        }

        Assert.True(game.HasFiveInARow(1));
        Assert.False(game.HasFiveInARow(2));
    }

    [Fact]
    public void Omok_FourInARow_DoesNotWin()
    {
        MiniGame game = NewOmok(Player(1, "A"));
        game.StartRound();
        for (int i = 0; i < 4; i++)
        {
            game.TryPlacePiece(3 + i, 3, 1);
        }

        Assert.False(game.HasFiveInARow(1));
    }

    [Fact]
    public void Omok_OccupiedSquare_IsRejected()
    {
        MiniGame game = NewOmok(Player(1, "A"));
        game.StartRound();
        Assert.True(game.TryPlacePiece(7, 7, 1));
        Assert.False(game.TryPlacePiece(7, 7, 2));
        Assert.False(game.TryPlacePiece(15, 0, 1)); // out of bounds
    }

    [Theory]
    [InlineData(0, 12, 6)]
    [InlineData(1, 20, 10)]
    [InlineData(2, 30, 15)]
    public void MatchCard_BoardSizes(int piece, int cards, int pairs)
    {
        var game = new MiniGame(1, MiniGame.TypeMatchCard, Player(1, "A"), "r", "", piece);
        game.StartRound();

        Assert.Equal(cards, game.CardCount);
        Assert.Equal(pairs, game.MatchesToWin);
        Assert.Equal(cards, game.Cards.Count);
        // Every card id appears exactly twice.
        Assert.All(game.Cards.GroupBy(x => x), g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public void Records_StoreAsQuestCustomData_AndScore()
    {
        FieldPlayer owner = Player(1, "A");
        MiniGame game = NewOmok(owner);

        game.AddResult(0, MiniGame.ResultWin);
        game.AddResult(0, MiniGame.ResultWin);
        game.AddResult(0, MiniGame.ResultLose);
        game.AddResult(0, MiniGame.ResultTie);

        Character c = owner.Character;
        Assert.Equal("1,1,2", c.StartedQuests[MiniGame.OmokRecordQuest]); // losses,ties,wins
        Assert.Equal(2, game.Wins(c));
        Assert.Equal(1, game.Ties(c));
        Assert.Equal(1, game.Losses(c));
        Assert.Equal(2000 + 2 * 2 + 1 - 2, game.Score(c));
    }

    [Fact]
    public void Registry_TracksParticipants()
    {
        var registry = new MiniGameRegistry();
        FieldPlayer owner = Player(1, "A");
        FieldPlayer visitor = Player(2, "B");

        MiniGame game = registry.Create(MiniGame.TypeOmok, owner, "r", "", 0);
        Assert.Same(game, registry.Get(game.ObjectId));
        Assert.Same(game, registry.GetForCharacter(1));
        Assert.Single(registry.GamesInMap(100000000));

        registry.SetVisitor(game, visitor);
        Assert.Same(game, registry.GetForCharacter(2));

        registry.RemoveVisitor(game);
        Assert.Null(registry.GetForCharacter(2));
        Assert.Null(game.Visitor);

        registry.Remove(game);
        Assert.Null(registry.Get(game.ObjectId));
        Assert.Empty(registry.GamesInMap(100000000));
    }

    [Fact]
    public void Balloon_EncodesInteractionBlock()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        FieldPlayer owner = Player(7, "A");
        MiniGame game = NewOmok(owner, piece: 2);

        byte[] p = packets.MiniRoomBalloon(7, game);
        int i = 2; // opcode
        Assert.Equal(7, BitConverter.ToInt32(p, i)); i += 4;   // owner id
        Assert.Equal(1, p[i++]);                               // game type (omok)
        Assert.Equal(game.ObjectId, BitConverter.ToInt32(p, i)); i += 4;
        i += 2 + BitConverter.ToInt16(p, i);                   // description string
        Assert.Equal(0, p[i++]);                               // no password
        Assert.Equal(2, p[i++]);                               // piece icon (itemId % 10)
        Assert.Equal(1, p[i++]);                               // current size (owner only)
        Assert.Equal(2, p[i++]);                               // max size
        Assert.Equal(0, p[i++]);                               // lobby (no round running)
        Assert.Equal(p.Length, i);

        byte[] cleared = packets.MiniRoomBalloon(7, null);
        Assert.Equal(7, BitConverter.ToInt32(cleared, 2));
        Assert.Equal(0, cleared[6]);
        Assert.Equal(7, cleared.Length);
    }

    // ---- e2e: create -> join via balloon -> ready -> start -> five stones -> result ----

    private static byte[] MigrateIn(MapleSession session, int characterId)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteInt(characterId);
        w.WriteBytes(new byte[16]);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteLong(0);
        return w.ToArray();
    }

    private static byte[] MiniRoomPacket(MapleSession session, Action<PacketWriter> body)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MiniRoom), session.Config.PacketHeaderSize, session.Config.CodePage);
        body(w);
        return w.ToArray();
    }

    /// <summary>Creates an Omok room on entry, starts once the visitor readies, then wins.</summary>
    private sealed class Host : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private bool _created;
        private int _stonesPlaced;

        public Host(int characterId) => _characterId = characterId;

        public TaskCompletionSource RoomOpen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_created)
            {
                _created = true;
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(0);        // MRP_Create
                    w.WriteByte(1);        // omok room
                    w.WriteString("come play");
                    w.WriteByte(0);        // no password
                    w.WriteByte(0);        // piece set 0 (item 4080000)
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5) // the room opened for us
                {
                    RoomOpen.TrySetResult();
                }
                else if (op == 55) // visitor is ready -> start
                {
                    await session.SendAsync(MiniRoomPacket(session, w => w.WriteByte(58)));
                }
                else if (op == 58) // round started -> place a row of five
                {
                    await PlaceStone(session);
                }
                else if (op == 61) // our stone echoed -> place the next
                {
                    if (++_stonesPlaced < 5)
                    {
                        await PlaceStone(session);
                    }
                }
                else if (op == 59) // game result
                {
                    Result.TrySetResult(p.ReadByte());
                }
            }
        }

        private ValueTask PlaceStone(MapleSession session)
            => new(session.SendAsync(MiniRoomPacket(session, w =>
            {
                w.WriteByte(61);               // ORP_PutStoneChecker
                w.WriteInt(3 + _stonesPlaced); // x marches right
                w.WriteInt(7);
                w.WriteByte(1);                // stone type
            })).AsTask());
    }

    /// <summary>Joins the room from its balloon and readies up.</summary>
    private sealed class Challenger : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private readonly int _opBalloon = ServerOps.Get(ServerOpcode.UserMiniRoomBalloon);
        private bool _joined;

        public Challenger(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int Result, int Winner)> Outcome { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opBalloon && !_joined)
            {
                _joined = true;
                p.ReadInt();               // owner id
                p.ReadByte();              // game type
                int objectId = p.ReadInt();
                await session.SendAsync(MiniRoomPacket(session, w =>
                {
                    w.WriteByte(4);        // MRP_Enter
                    w.WriteInt(objectId);
                    w.WriteByte(0);        // no password entered
                }));
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 5) // we're in the room -> ready up
                {
                    await session.SendAsync(MiniRoomPacket(session, w => w.WriteByte(55)));
                }
                else if (op == 59)
                {
                    int result = p.ReadByte();
                    int winner = result != 1 ? p.ReadByte() : -1;
                    Outcome.TrySetResult((result, winner));
                }
            }
        }
    }

    [Fact]
    public async Task Omok_CreateJoinPlay_OwnerWinsWithFiveInARow()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });
        alice.EquippedItems.Add(new InventoryItem { ItemId = 4080000, Position = 1, Quantity = 1, CharacterId = alice.Id });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var miniGames = new MiniGameRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Challenger(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, miniGames: miniGames);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Host(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, miniGames: miniGames);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        await aliceClient.RoomOpen.Task.WaitAsync(cts.Token);

        (int result, int winner) = await bobClient.Outcome.Task.WaitAsync(cts.Token);
        Assert.Equal(2, result); // a win…
        Assert.Equal(0, winner); // …for the owner's seat

        Assert.Equal(2, await aliceClient.Result.Task.WaitAsync(cts.Token));
        Assert.Equal("0,0,1", alice.StartedQuests[MiniGame.OmokRecordQuest]); // W for Alice
        Assert.Equal("1,0,0", bob.StartedQuests[MiniGame.OmokRecordQuest]);   // L for Bob
    }
}
