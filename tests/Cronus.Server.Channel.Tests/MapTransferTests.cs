using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Map transfer: a direct map-id transfer moves the player between fields (SetField
/// map-change branch, leave/enter announcements); a portal-by-name request is refused with
/// LP_TransferFieldReqIgnored until wz portal data exists.
/// </summary>
public class MapTransferTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class TransferClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opIgnored = ServerOps.Get(ServerOpcode.TransferFieldReqIgnored);

        private int _setFieldCount;

        public TransferClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }

        public TaskCompletionSource<bool> EnteredGame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int MapId, short Hp)> MapChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<byte> TransferRefused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = NewPacket(session, ClientOpcode.MigrateIn);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                _setFieldCount++;
                if (_setFieldCount == 1)
                {
                    EnteredGame.TrySetResult(true);
                    return ValueTask.CompletedTask;
                }

                // Map-change branch: decode through to the map id + HP.
                p.ReadShort();               // ClientOptMan
                p.ReadInt();                 // channel
                p.ReadByte();
                p.ReadInt();                 // old driver id
                p.ReadByte();                // portal count
                Assert.Equal(0, p.ReadByte()); // bCharacterData = false
                p.ReadShort();               // notifier
                p.ReadByte();                // clear stat
                int mapId = p.ReadInt();
                p.ReadByte();                // portal
                short hp = p.ReadShort();
                p.ReadLong();                // ftServer
                Assert.Equal(0, p.Remaining);
                MapChanged.TrySetResult((mapId, hp));
            }
            else if (opcode == _opIgnored)
            {
                TransferRefused.TrySetResult(p.ReadByte());
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask ChatAsync(string message)
        {
            var w = NewPacket(Session!, ClientOpcode.UserChat);
            w.WriteInt(0);           // timestamp
            w.WriteString(message);
            w.WriteByte(0);          // only balloon
            await Session!.SendAsync(w.ToArray());
        }

        /// <summary>Steps on a scripted portal (CP_UserPortalScriptRequest).</summary>
        public async ValueTask RequestPortalScriptAsync(string portalName)
        {
            var w = NewPacket(Session!, ClientOpcode.UserPortalScriptRequest);
            w.WriteByte(0);              // portal count
            w.WriteString(portalName);
            w.WriteShort(0);
            w.WriteShort(0);
            await Session!.SendAsync(w.ToArray());
        }

        /// <summary>The tombstone dismissal: mapId 0, no portal, trailing revive type.</summary>
        public async ValueTask RequestReviveAsync(byte reviveType)
        {
            var w = NewPacket(Session!, ClientOpcode.UserTransferFieldRequest);
            w.WriteByte(0);              // portal count
            w.WriteInt(0);               // mapId 0 = revive
            w.WriteString(string.Empty);
            w.WriteByte(0);              // unk
            w.WriteByte(reviveType);
            await Session!.SendAsync(w.ToArray());
        }

        public async ValueTask RequestTransferAsync(int mapId, string portalName)
        {
            var w = NewPacket(Session!, ClientOpcode.UserTransferFieldRequest);
            w.WriteByte(1);                  // portal count
            w.WriteInt(mapId);
            w.WriteString(portalName);
            if (portalName.Length > 0)
            {
                w.WriteShort(0);             // x
                w.WriteShort(0);             // y
            }

            w.WriteByte(0);                  // unk
            w.WriteByte(0);                  // revive type
            await Session!.SendAsync(w.ToArray());
        }

        private static PacketWriter NewPacket(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    /// <summary>Polls until <paramref name="condition"/> holds (the token bounds the wait).</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            await Task.Delay(10, ct);
        }
    }

    private static (MapleSession Server, MapleSession Client) Wire(
        TransferClient client, ChannelHandler handler, CancellationToken ct)
    {
        var config = ServerConfig.Jms186;
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server, handler);
        var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        _ = serverSession.RunAsync(ct);
        _ = clientSession.RunAsync(ct);
        return (serverSession, clientSession);
    }

    [Fact]
    public async Task DirectMapIdTransfer_MovesBetweenFields()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Mover", MapId = 100000000, Hp = 50,
        });

        var fields = new FieldRegistry();
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        // SetField is sent before the server-side field join completes; wait for the join.
        await WaitUntilAsync(() => fields.Get(100000000).Players.Any(fp => fp.Character.Id == hero.Id), cts.Token);
        Assert.Contains(fields.Get(100000000).Players, fp => fp.Character.Id == hero.Id);

        await client.RequestTransferAsync(mapId: 104040000, portalName: string.Empty);
        (int mapId, short hp) = await client.MapChanged.Task.WaitAsync(cts.Token);

        Assert.Equal(104040000, mapId);
        Assert.Equal(50, hp);
        Assert.DoesNotContain(fields.Get(100000000).Players, fp => fp.Character.Id == hero.Id);
        Assert.Contains(fields.Get(104040000).Players, fp => fp.Character.Id == hero.Id);
        Assert.Equal(104040000, hero.MapId); // character state updated
    }

    // Verifies the transfer is persisted (a DB-backed repo would otherwise lose the new map).
    private sealed class CountingRepository : ICharacterRepository
    {
        private readonly InMemoryCharacterRepository _inner = new();
        public int SaveCount { get; private set; }

        public IReadOnlyList<Character> ListByAccount(int accountId, int worldId) => _inner.ListByAccount(accountId, worldId);
        public Character? Find(int characterId) => _inner.Find(characterId);
        public bool NameExists(string name) => _inner.NameExists(name);

        public Character? FindByName(string name) => _inner.FindByName(name);
        public IReadOnlyList<Character> ListByGuild(int guildId) => _inner.ListByGuild(guildId);
        public Character Create(Character character) => _inner.Create(character);
        public bool Delete(int characterId) => _inner.Delete(characterId);
        public void Save(Character character) { SaveCount++; _inner.Save(character); }
    }

    [Fact]
    public async Task Transfer_PersistsNewMap()
    {
        var repo = new CountingRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Saver", MapId = 100000000, Hp = 50,
        });

        var fields = new FieldRegistry();
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.RequestTransferAsync(mapId: 104040000, portalName: string.Empty);
        await client.MapChanged.Task.WaitAsync(cts.Token);

        Assert.True(repo.SaveCount >= 1); // the transfer flushed the character
        Assert.Equal(104040000, hero.MapId);
    }

    [Fact]
    public async Task MapCommand_MovesPlayer()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Cmder", MapId = 100000000, Hp = 50,
        });

        var fields = new FieldRegistry();
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.ChatAsync("/map 104040000");

        (int mapId, short _) = await client.MapChanged.Task.WaitAsync(cts.Token);
        Assert.Equal(104040000, mapId);
        Assert.Equal(104040000, hero.MapId);
    }

    [Fact]
    public async Task PortalByName_ResolvesThroughMapData()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Walker", MapId = 100000000, Hp = 50,
        });

        // Map 100000000 has portal "east00" linking to 104040000 at target "west00".
        var start = new MapData
        {
            MapId = 100000000,
            Portals = new[]
            {
                new PortalData { Id = 0, Name = "sp", Type = 0, TargetMapId = MapData.NoLink },
                new PortalData { Id = 1, Name = "east00", Type = 2, TargetMapId = 104040000, TargetName = "west00" },
            },
        };
        var destination = new MapData
        {
            MapId = 104040000,
            Portals = new[]
            {
                new PortalData { Id = 0, Name = "sp", Type = 0, TargetMapId = MapData.NoLink },
                new PortalData { Id = 3, Name = "west00", Type = 2, TargetMapId = MapData.NoLink },
            },
        };
        var maps = new InMemoryMapProvider(new[] { start, destination });

        var fields = new FieldRegistry();
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.RequestTransferAsync(mapId: -1, portalName: "east00");

        (int mapId, short _) = await client.MapChanged.Task.WaitAsync(cts.Token);
        Assert.Equal(104040000, mapId);
        Assert.Equal(104040000, hero.MapId);
        Assert.Equal(3, hero.Portal); // spawned at the "west00" portal id
        Assert.Contains(fields.Get(104040000).Players, fp => fp.Character.Id == hero.Id);
    }

    [Fact]
    public async Task PortalByName_IsRefusedWhenMapDataMissing()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Blocked", MapId = 100000000,
        });

        var fields = new FieldRegistry();
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.RequestTransferAsync(mapId: -1, portalName: "east00");

        byte reason = await client.TransferRefused.Task.WaitAsync(cts.Token);
        Assert.Equal(1, reason); // TF_DISABLED_PORTAL
        Assert.Contains(fields.Get(100000000).Players, fp => fp.Character.Id == hero.Id); // unmoved
    }

    [Fact]
    public async Task PortalScriptRequest_WithoutAScript_IsRefusedNotIgnored()
    {
        // The request locks the client until SetField or the refusal arrives — silence wedges it.
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Stepper", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var maps = new InMemoryMapProvider(new[] { map });
        var fields = new FieldRegistry(maps);
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.RequestPortalScriptAsync("no_such_portal");

        byte reason = await client.TransferRefused.Task.WaitAsync(cts.Token);
        Assert.Equal(1, reason); // TF_DISABLED_PORTAL
    }

    [Fact]
    public async Task Revive_InPlace_WhenReviveTypePositive()
    {
        // revive_type > 0 revives on the death map (ports mapChangePortal); 0 goes to the
        // return town. Either way HP/MP come back full.
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Fallen", MapId = 100010000, Hp = 0, MaxHp = 500, MaxMp = 300,
        });

        var maps = new InMemoryMapProvider(new[]
        {
            new MapData { MapId = 100010000, ReturnMap = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
        });
        var fields = new FieldRegistry(maps);
        var client = new TransferClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;

        await client.EnteredGame.Task.WaitAsync(cts.Token);
        await client.RequestReviveAsync(reviveType: 1);

        (int mapId, short hp) = await client.MapChanged.Task.WaitAsync(cts.Token);
        Assert.Equal(100010000, mapId);      // revived where they died
        Assert.Equal(500, hp);               // full HP
        Assert.Equal(300, hero.Mp);          // full MP
    }
}
