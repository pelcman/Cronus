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
/// On game entry the server spawns the field's NPCs and mobs; this decodes LP_NpcEnterField and
/// LP_MobEnterField field-by-field to lock the JMS v186 layouts.
/// </summary>
public class FieldSpawnTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record NpcInfo(int Oid, int Template, short X, short Y, byte Facing, short Fh, byte Enabled, int Remaining);
    private sealed record MobInfo(int Oid, byte Control, int Template, short X, short Y, byte AppearType, int Remaining);

    private sealed class SpawnClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opNpc = ServerOps.Get(ServerOpcode.NpcEnterField);
        private readonly int _opMob = ServerOps.Get(ServerOpcode.MobEnterField);

        public SpawnClient(int characterId) => _characterId = characterId;

        public TaskCompletionSource<NpcInfo> Npc { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<MobInfo> Mob { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opNpc)
            {
                int oid = p.ReadInt();
                int template = p.ReadInt();
                short x = p.ReadShort();
                short y = p.ReadShort();
                byte facing = p.ReadByte();
                short fh = p.ReadShort();
                p.ReadShort();   // rx0
                p.ReadShort();   // rx1
                byte enabled = p.ReadByte();
                Npc.TrySetResult(new NpcInfo(oid, template, x, y, facing, fh, enabled, p.Remaining));
            }
            else if (opcode == _opMob)
            {
                int oid = p.ReadInt();
                byte control = p.ReadByte();
                int template = p.ReadInt();
                p.Skip(16);      // temporary-stat mask (4 ints)
                short x = p.ReadShort();
                short y = p.ReadShort();
                p.ReadByte();    // stance
                p.ReadShort();   // fh
                p.ReadShort();   // origin fh
                byte appear = p.ReadByte();
                p.ReadByte();    // carnival team
                p.ReadInt();     // effect item
                p.ReadInt();     // phase
                Mob.TrySetResult(new MobInfo(oid, control, template, x, y, appear, p.Remaining));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Entry_SpawnsNpcAndMob_WithExactLayout()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Scout", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Npcs = new[] { new NpcSpawn { TemplateId = 9010000, X = 120, Y = -60, Foothold = 7, Facing = 0, Rx0 = 100, Rx1 = 140 } },
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 300, Y = 0, Foothold = 12 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        var client = new SpawnClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        NpcInfo npc = await client.Npc.Task.WaitAsync(cts.Token);
        Assert.Equal(1_000_000, npc.Oid);   // NPC object-id base
        Assert.Equal(9010000, npc.Template);
        Assert.Equal(120, npc.X);
        Assert.Equal(-60, npc.Y);
        Assert.Equal(1, npc.Enabled);
        Assert.Equal(0, npc.Remaining);     // exact layout, nothing left over

        MobInfo mob = await client.Mob.Task.WaitAsync(cts.Token);
        Assert.Equal(2_000_000, mob.Oid);   // mob object-id base
        Assert.Equal(1, mob.Control);
        Assert.Equal(100100, mob.Template);
        Assert.Equal(300, mob.X);
        Assert.Equal(0xFF, mob.AppearType); // MOBAPPEAR_NORMAL (-1)
        Assert.Equal(0, mob.Remaining);
    }
}
