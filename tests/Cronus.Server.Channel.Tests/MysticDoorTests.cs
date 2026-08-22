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

public class MysticDoorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static MysticDoor MakeDoor() => new()
    {
        OwnerId = 7,
        SkillId = MysticDoor.SkillMysticDoor,
        FieldMapId = 104040000,
        FieldX = 120,
        FieldY = 60,
        FieldPortalId = 2,
        TownMapId = 100000000,
        TownX = -400,
        TownY = 33,
        TownPortalId = 5,
        ExpiresAt = DateTime.MaxValue,
    };

    [Fact]
    public void DoorPackets_HaveExactVanillaLayouts()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        MysticDoor door = MakeDoor();

        var created = new PacketReader(packets.TownPortalCreated(7, 120, 60, isTown: false), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.TownPortalCreated), created.ReadHeader());
        Assert.Equal(0, created.ReadByte());
        Assert.Equal(7, created.ReadInt());
        Assert.Equal((short)120, created.ReadShort());
        Assert.Equal((short)60, created.ReadShort());
        Assert.Equal(0, created.Remaining);

        var removed = new PacketReader(packets.TownPortalRemoved(7), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.TownPortalRemoved), removed.ReadHeader());
        Assert.Equal(1, removed.ReadByte());
        Assert.Equal(7, removed.ReadInt());
        Assert.Equal(0, removed.Remaining);

        var info = new PacketReader(packets.MysticDoorInfo(door), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.TownPortal), info.ReadHeader());
        Assert.Equal(104040000, info.ReadInt()); // field map
        Assert.Equal(100000000, info.ReadInt()); // town map
        Assert.Equal(MysticDoor.SkillMysticDoor, info.ReadInt());
        Assert.Equal((short)-400, info.ReadShort());
        Assert.Equal((short)33, info.ReadShort());
        Assert.Equal(0, info.Remaining);

        var reset = new PacketReader(packets.MysticDoorInfo(null), ServerConfig.Jms186.CodePage);
        reset.ReadHeader();
        Assert.Equal(999999999, reset.ReadInt());
        Assert.Equal(999999999, reset.ReadInt());
        Assert.Equal(0, reset.Remaining);

        var party = new PacketReader(packets.PartyTownPortalChanged(door), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.PartyResult), party.ReadHeader());
        Assert.Equal(46, party.ReadByte());       // PartyInfo_TownPortalChanged
        Assert.Equal(5, party.ReadByte());        // the town door-portal number
        Assert.Equal(104040000, party.ReadInt());
        Assert.Equal(100000000, party.ReadInt());
        Assert.Equal(MysticDoor.SkillMysticDoor, party.ReadInt());
        Assert.Equal((short)-400, party.ReadShort());
        Assert.Equal((short)33, party.ReadShort());
        Assert.Equal(0, party.Remaining);
    }

    [Fact]
    public void DoorSides_ResolveTargetsAndPositions()
    {
        MysticDoor door = MakeDoor();
        Assert.Equal(100000000, door.TargetMapFor(104040000)); // field -> town
        Assert.Equal(104040000, door.TargetMapFor(100000000)); // town -> field
        Assert.Equal(5, door.TargetPortalFor(104040000));      // arriving in town: its door spot
        Assert.Equal(2, door.TargetPortalFor(100000000));      // arriving in field: nearest portal
        Assert.Equal(((short)-400, (short)33), door.PositionIn(100000000));
        Assert.True(door.IsTownSide(100000000));
    }

    private sealed class DoorSkillProvider : ISkillProvider
    {
        public int GetMaxLevel(int skillId) => 20;

        public SkillEffect? GetSkillEffect(int skillId, int level)
            => new() { MpCon = 0, DurationMs = 180_000 };

        public MobSkillData? GetMobSkill(int skillId, int level) => null;

        public IReadOnlyList<int> GetSkillIds(int jobId) => Array.Empty<int>();
    }

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

    /// <summary>Migrates in and casts Mystic Door once the field is up; flags the door packets.</summary>
    private sealed class Priest : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opCreated = ServerOps.Get(ServerOpcode.TownPortalCreated);
        private readonly int _opInfo = ServerOps.Get(ServerOpcode.TownPortal);
        private bool _cast;

        public Priest(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(bool IsTown, int OwnerId)> DoorCreated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(int FieldMap, int TownMap)> Info { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_cast)
            {
                _cast = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSkillUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteInt(MysticDoor.SkillMysticDoor);
                w.WriteByte(10);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opCreated)
            {
                bool isTown = p.ReadByte() == 1;
                DoorCreated.TrySetResult((isTown, p.ReadInt()));
            }
            else if (opcode == _opInfo)
            {
                Info.TrySetResult((p.ReadInt(), p.ReadInt()));
            }
        }
    }

    [Fact]
    public async Task CastingMysticDoor_OpensBothSides()
    {
        var repo = new InMemoryCharacterRepository();
        Character priest = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Cleric", MapId = 104040000, Level = 75, Job = 230 });
        priest.Skills[MysticDoor.SkillMysticDoor] = 10;

        var town = new MapData
        {
            MapId = 100000000,
            Portals = new[]
            {
                new PortalData { Id = 0, Type = 0, Name = "sp", X = 0, Y = 0 },
                new PortalData { Id = 5, Type = 6, Name = "door00", X = -400, Y = 33 },
            },
        };
        var hunting = new MapData
        {
            MapId = 104040000,
            Portals = new[] { new PortalData { Id = 0, Type = 0, Name = "sp", X = 10, Y = 10 } },
            ReturnMap = 100000000,
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { town, hunting }));

        using var cts = new CancellationTokenSource(Timeout);

        var priestClient = new Priest(priest.Id);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, fields,
            new InMemoryMapProvider(new[] { town, hunting }), skills: new DoorSkillProvider());

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, priestClient);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        (bool isTown, int ownerId) = await priestClient.DoorCreated.Task.WaitAsync(cts.Token);
        Assert.False(isTown); // the caster's own map sees the field side
        Assert.Equal(priest.Id, ownerId);

        (int fieldMap, int townMap) = await priestClient.Info.Task.WaitAsync(cts.Token);
        Assert.Equal(104040000, fieldMap);
        Assert.Equal(100000000, townMap);

        // Both sides are registered, and the town side sits on the type-6 portal spot.
        MysticDoor? standing = fields.Get(100000000).FindDoorByOwner(priest.Id);
        Assert.NotNull(standing);
        Assert.Equal(5, standing!.TownPortalId);
        Assert.Equal((short)-400, standing.TownX);
        Assert.NotNull(fields.Get(104040000).FindDoorByOwner(priest.Id));
    }
}
