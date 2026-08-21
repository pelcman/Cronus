using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class PartyLivenessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const int OtherMap = 200000000;

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

    /// <summary>Alice: forms a party, then warps to another map (via the !map command).</summary>
    private sealed class Warper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _partnerName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private bool _invited;
        private bool _warped;

        public Warper(int characterId, string partnerName)
        {
            _characterId = characterId;
            _partnerName = partnerName;
        }

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                await SendParty(session, w => w.WriteByte(1)); // create
            }
            else if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 8 && !_invited)
                {
                    _invited = true;
                    await SendParty(session, w => { w.WriteByte(4); w.WriteString(_partnerName); });
                }
                else if (op == 15 && !_warped) // partner joined -> warp away
                {
                    _warped = true;
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteInt(0);                       // timestamp
                    w.WriteString("!map " + OtherMap);   // GM command
                    w.WriteBool(false);                  // onlyBalloon
                    await session.SendAsync(w.ToArray());
                }
            }
        }

        private static async ValueTask SendParty(MapleSession session, Action<PacketWriter> body)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            body(w);
            await session.SendAsync(w.ToArray());
        }
    }

    /// <summary>Bob: joins on invite, then reads the leader's map from the silent party update.</summary>
    private sealed class MapWatcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _leaderId;
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);

        public MapWatcher(int characterId, int leaderId)
        {
            _characterId = characterId;
            _leaderId = leaderId;
        }

        public TaskCompletionSource<int> LeaderMap { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode != _opParty)
            {
                return;
            }

            int op = p.ReadByte();
            if (op == 4) // invite popup -> join
            {
                int partyId = p.ReadInt();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(3);
                w.WriteInt(partyId);
                await session.SendAsync(w.ToArray());
            }
            else if (op == 7) // silent update: [partyId][6 ids][6 names*13][6 jobs][6 levels][6 chans][leader][6 maps]...
            {
                p.ReadInt(); // party id
                int leaderSlot = -1;
                for (int i = 0; i < 6; i++)
                {
                    if (p.ReadInt() == _leaderId)
                    {
                        leaderSlot = i;
                    }
                }

                p.Skip(6 * 13); // names
                p.Skip(6 * 4);  // jobs
                p.Skip(6 * 4);  // levels
                p.Skip(6 * 4);  // channels
                p.ReadInt();    // leader id
                int leaderMap = 0;
                for (int i = 0; i < 6; i++)
                {
                    int map = p.ReadInt();
                    if (i == leaderSlot)
                    {
                        leaderMap = map;
                    }
                }

                LeaderMap.TrySetResult(leaderMap);
            }
        }
    }

    [Fact]
    public async Task PartyMember_ChangingMap_RefreshesTheWindow()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var maps = new[]
        {
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = OtherMap, Portals = Array.Empty<PortalData>() },
        };
        var mapProvider = new InMemoryMapProvider(maps);
        var fields = new FieldRegistry(mapProvider);
        var parties = new PartyRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new MapWatcher(bob.Id, alice.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider, channelId: 0, parties: parties);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);

        var aliceClient = new Warper(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider, channelId: 0, parties: parties);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int leaderMap = await bobClient.LeaderMap.Task.WaitAsync(cts.Token);
        Assert.Equal(OtherMap, leaderMap); // Bob's window now shows Alice on the new map
    }
}
