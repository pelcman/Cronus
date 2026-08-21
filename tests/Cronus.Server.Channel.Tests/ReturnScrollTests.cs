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

public class ReturnScrollTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    /// <summary>Migrates in, uses a return scroll on first field entry, flags on the second SetField (the warp).</summary>
    private sealed class ScrollUser : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _scrollId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private int _setFieldCount;

        public ScrollUser(int characterId, int scrollId)
        {
            _characterId = characterId;
            _scrollId = scrollId;
        }

        public TaskCompletionSource Warped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode != _opSetField)
            {
                return;
            }

            _setFieldCount++;
            if (_setFieldCount == 1)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserStatChangeItemUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);          // timestamp
                w.WriteShort(1);        // USE slot 1
                w.WriteInt(_scrollId);
                await session.SendAsync(w.ToArray());
            }
            else
            {
                Warped.TrySetResult(); // the second SetField is the scroll warp
            }
        }
    }

    [Fact]
    public async Task UsingReturnScroll_WarpsAndConsumesIt()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Rogue", MapId = 100000000 });
        hero.EquippedItems.Add(new InventoryItem { ItemId = 2030001, Position = 1, Quantity = 1, CharacterId = hero.Id });
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // The scroll returns to Ellinia (104000000).
        var items = new InMemoryItemProvider(new[] { new ConsumeSpec { ItemId = 2030001, MoveTo = 104000000 } });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new ScrollUser(hero.Id, 2030001);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, items: items);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Warped.Task.WaitAsync(cts.Token);

        Assert.Equal(104000000, hero.MapId);                       // warped to the scroll's target
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 2030001); // scroll consumed
    }
}
