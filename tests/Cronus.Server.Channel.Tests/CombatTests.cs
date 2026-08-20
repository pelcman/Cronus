using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class CombatTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // Builds a JMS v186 CP_UserMeleeAttack hitting one mob with the given damages.
    private static byte[] BuildMeleeAttack(MapleSession session, int mobOid, int[] damages)
    {
        int damagePerMob = damages.Length & 0x0F;
        int mobCount = 1;
        int hitKey = damagePerMob | (mobCount << 4);

        var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteByte(0);            // FieldKey
        w.WriteInt(0);             // dr0
        w.WriteInt(0);             // dr1
        w.WriteByte((byte)hitKey);
        w.WriteInt(0);             // dr2
        w.WriteInt(0);             // dr3
        w.WriteInt(0);             // skill id
        w.WriteInt(0);             // dr rand
        w.WriteInt(0);             // dr crc
        w.WriteInt(0);             // crc
        w.WriteByte(0);            // buff key
        w.WriteShort(0);           // attack action key
        w.WriteByte(0);            // attack action type
        w.WriteByte(0);            // attack speed
        w.WriteInt(0);             // attack time
        w.WriteInt(0);             // dwID
        // one target:
        w.WriteInt(mobOid);
        w.WriteBytes(new byte[4]);  // hit/fore/frame/statIndex
        w.WriteBytes(new byte[8]);  // 4 mob shorts
        w.WriteShort(0);            // tDelay
        foreach (int d in damages)
        {
            w.WriteInt(d);
        }

        w.WriteInt(0);              // mob crc
        return w.ToArray();
    }

    private sealed class Fighter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int[] _damages;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opMobLeave = ServerOps.Get(ServerOpcode.MobLeaveField);

        private int _mobOid = -1;
        private bool _setField;

        public Fighter(int characterId, int[] damages)
        {
            _characterId = characterId;
            _damages = damages;
        }

        public TaskCompletionSource<(int Oid, byte DeadType)> MobKilled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField)
            {
                _setField = true;
                await MaybeAttack(session);
            }
            else if (opcode == _opMobEnter)
            {
                _mobOid = p.ReadInt();
                await MaybeAttack(session);
            }
            else if (opcode == _opMobLeave)
            {
                int oid = p.ReadInt();
                byte deadType = p.ReadByte();
                MobKilled.TrySetResult((oid, deadType));
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (_setField && _mobOid >= 0)
            {
                await session.SendAsync(BuildMeleeAttack(session, _mobOid, _damages));
            }
        }
    }

    [Fact]
    public async Task MeleeAttack_KillsMob_AndBroadcastsLeave()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Warrior", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // Two hits of 40 each = 80 > 50 HP -> dead.
        var client = new Fighter(hero.Id, new[] { 40, 40 });
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

        (int oid, byte deadType) = await client.MobKilled.Task.WaitAsync(cts.Token);

        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);
        Assert.Equal(mob.ObjectId, oid);
        Assert.Equal(1, deadType);
        Assert.True(mob.IsDead);
        Assert.Equal(0, mob.Hp);
    }

    [Fact]
    public void ParseMelee_ReadsTargetsAndDamages()
    {
        var config = ServerConfig.Jms186;
        // Craft a buffer with 1 mob and 3 hits.
        var pw = new PacketWriter(encoding: config.CodePage);
        pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0x13);            // damagePerMob=3, mobCount=1
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteInt(0);               // skill
        pw.WriteInt(0); pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0); pw.WriteShort(0); pw.WriteByte(0); pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0); // attack time, dwID
        pw.WriteInt(7777);            // mob oid
        pw.WriteBytes(new byte[4]);
        pw.WriteBytes(new byte[8]);
        pw.WriteShort(0);
        pw.WriteInt(100); pw.WriteInt(200); pw.WriteInt(300); // 3 damages
        pw.WriteInt(0);               // mob crc

        var reader = new PacketReader(pw.ToArray(), config.CodePage);
        AttackInfo info = AttackParser.ParseMelee(reader);

        AttackTarget t = Assert.Single(info.Targets);
        Assert.Equal(7777, t.MobObjectId);
        Assert.Equal(new[] { 100, 200, 300 }, t.Damages);
        Assert.Equal(600, t.TotalDamage);
        Assert.Equal(0, reader.Remaining);
    }
}
