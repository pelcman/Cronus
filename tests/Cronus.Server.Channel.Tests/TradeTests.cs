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

public class TradeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

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

    private static PacketWriter MiniRoom(MapleSession session)
        => new(ClientOps.Get(ClientOpcode.MiniRoom), session.Config.PacketHeaderSize, session.Config.CodePage);

    /// <summary>Starter: creates the room, invites Bob, stages a potion stack, then confirms.</summary>
    private sealed class Alice : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _partnerId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);
        private bool _created;
        private bool _put;

        public Alice(int characterId, int partnerId)
        {
            _characterId = characterId;
            _partnerId = partnerId;
        }

        public TaskCompletionSource<byte> Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_created)
            {
                _created = true;
                var create = MiniRoom(session);
                create.WriteByte(0);  // MRP_Create
                create.WriteByte(3);  // trade room
                await session.SendAsync(create.ToArray());

                var invite = MiniRoom(session);
                invite.WriteByte(2);  // MRP_Invite
                invite.WriteInt(_partnerId);
                await session.SendAsync(invite.ToArray());
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 4 && !_put) // partner entered -> stage the potions
                {
                    _put = true;
                    var put = MiniRoom(session);
                    put.WriteByte(13);   // TRP_PutItem
                    put.WriteByte(2);    // USE tab
                    put.WriteShort(1);   // inventory slot
                    put.WriteShort(5);   // whole stack
                    put.WriteByte(1);    // trade-window slot
                    await session.SendAsync(put.ToArray());
                }
                else if (op == 14 && p.ReadByte() == 1) // partner staged meso -> confirm
                {
                    var confirm = MiniRoom(session);
                    confirm.WriteByte(15); // TRP_Trade
                    await session.SendAsync(confirm.ToArray());
                }
                else if (op == 10) // leave: [slot][message]
                {
                    p.ReadByte();
                    Closed.TrySetResult(p.ReadByte());
                }
            }
        }
    }

    /// <summary>Visitor: accepts the invite, stages meso, and confirms after Alice does.</summary>
    private sealed class Bob : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMiniRoom = ServerOps.Get(ServerOpcode.MiniRoom);

        public Bob(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<byte> Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opMiniRoom)
            {
                byte op = p.ReadByte();
                if (op == 2) // MRP_Invite -> enter the room
                {
                    var enter = MiniRoom(session);
                    enter.WriteByte(4);  // MRP_Enter
                    enter.WriteInt(0);   // miniroom id (unused for trades)
                    await session.SendAsync(enter.ToArray());
                }
                else if (op == 5) // entered -> stage 300 meso
                {
                    var money = MiniRoom(session);
                    money.WriteByte(14); // TRP_PutMoney
                    money.WriteInt(300);
                    await session.SendAsync(money.ToArray());
                }
                else if (op == 15) // Alice confirmed -> confirm too
                {
                    var confirm = MiniRoom(session);
                    confirm.WriteByte(15);
                    await session.SendAsync(confirm.ToArray());
                }
                else if (op == 10)
                {
                    p.ReadByte();
                    Closed.TrySetResult(p.ReadByte());
                }
            }
        }
    }

    [Fact]
    public async Task Trade_ExchangesItemsAndMeso()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Meso = 0 });
        alice.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 5, CharacterId = alice.Id });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Meso = 1000 });
        repo.Save(alice);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var trades = new TradeRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Bob(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, trades: trades);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Alice(alice.Id, bob.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, trades: trades);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        byte aliceMsg = await aliceClient.Closed.Task.WaitAsync(cts.Token);
        byte bobMsg = await bobClient.Closed.Task.WaitAsync(cts.Token);

        Assert.Equal(7, aliceMsg); // success
        Assert.Equal(7, bobMsg);

        Assert.Equal(300, alice.Meso);                                        // Bob's meso arrived
        Assert.Equal(700, bob.Meso);                                          // minus what he staged
        Assert.DoesNotContain(alice.EquippedItems, i => i.ItemId == 2000000); // potions left Alice
        InventoryItem potions = Assert.Single(bob.EquippedItems, i => i.ItemId == 2000000);
        Assert.Equal(5, potions.Quantity);                                    // and reached Bob
    }
}
