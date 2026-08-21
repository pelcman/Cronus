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

public class PetTests
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

    /// <summary>Summons the pet in cash slot 1 on entry, dismisses it after the spawn echo.</summary>
    private sealed class PetOwner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opActivated = ServerOps.Get(ServerOpcode.PetActivated);
        private bool _summoned;

        public PetOwner(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int ItemId, string Name)> Spawned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Dismissed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_summoned)
            {
                _summoned = true;
                await SendActivate(session);
            }
            else if (opcode == _opActivated)
            {
                p.ReadInt();               // character id
                p.ReadInt();               // pet index
                bool spawn = p.ReadByte() != 0;
                if (spawn)
                {
                    p.ReadByte();
                    int itemId = p.ReadInt();
                    string name = p.ReadString();
                    Spawned.TrySetResult((itemId, name));
                    await SendActivate(session); // toggle again = dismiss
                }
                else
                {
                    Dismissed.TrySetResult();
                }
            }
        }

        private async ValueTask SendActivate(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserActivatePetRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(0);
            w.WriteShort(1); // cash slot 1
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task SummonAndDismiss_RoundTripsThroughTheField()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });
        hero.EquippedItems.Add(new InventoryItem
        {
            ItemId = 5000000, Position = 1, Quantity = 1, CharacterId = hero.Id, PetName = "タマ",
        });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new PetOwner(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int itemId, string name) = await client.Spawned.Task.WaitAsync(cts.Token);
        Assert.Equal(5000000, itemId);
        Assert.Equal("タマ", name);

        await client.Dismissed.Task.WaitAsync(cts.Token);
        FieldPlayer player = fields.Get(100000000).Players.Single();
        Assert.Null(player.Pet);
    }
}
