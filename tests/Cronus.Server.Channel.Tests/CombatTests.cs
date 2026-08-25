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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

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

        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private readonly int _opDropEnter = ServerOps.Get(ServerOpcode.DropEnterField);
        private int _mobOid = -1;
        private bool _setField;

        public Fighter(int characterId, int[] damages)
        {
            _characterId = characterId;
            _damages = damages;
        }

        public TaskCompletionSource<(int Oid, byte DeadType)> MobKilled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> ExpGained { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> MesoGained { get; } =
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
            else if (opcode == _opDropEnter)
            {
                p.ReadByte();          // enter type
                int dropOid = p.ReadInt();
                // Request pickup of the meso drop.
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.DropPickUpRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);
                w.WriteInt(0);
                w.WriteShort(0);
                w.WriteShort(0);
                w.WriteInt(dropOid);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();          // unlock
                int mask = p.ReadInt();
                if (mask == 0)
                {
                    return; // entry updateStat (no stat fields) - skip
                }

                int value = p.ReadInt();
                if ((mask & 0x10000) != 0)       // Exp
                {
                    ExpGained.TrySetResult(value);
                }
                else if ((mask & 0x40000) != 0)  // Meso
                {
                    MesoGained.TrySetResult(value);
                }
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
        // wz mob stats override the spawn placeholder: 50 HP, 10 exp (below the level-2 threshold).
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 10 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);

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
        int exp = await client.ExpGained.Task.WaitAsync(cts.Token);

        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);
        Assert.Equal(mob.ObjectId, oid);
        Assert.Equal(1, deadType);
        Assert.True(mob.IsDead);
        Assert.Equal(0, mob.Hp);
        Assert.Equal(10, exp);          // mob exp granted (no level-up)
        Assert.Equal(10, hero.Exp);

        // The kill dropped meso (50 HP / 5 = 10), which the client picked up.
        int meso = await client.MesoGained.Task.WaitAsync(cts.Token);
        Assert.Equal(10, meso);
        Assert.Equal(10, hero.Meso);
    }

    private sealed class Watcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opAttack = ServerOps.Get(ServerOpcode.UserMeleeAttack);

        public Watcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int Attacker, int MobOid, int Damage)> SawAttack { get; } =
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

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opAttack)
            {
                int attacker = p.ReadInt();
                p.ReadByte();  // hit key
                p.ReadByte();  // level
                p.ReadByte();  // skill level
                p.ReadByte();  // buff key
                p.ReadShort(); // attack action
                p.ReadByte();  // speed
                p.ReadByte();  // mastery
                p.ReadInt();   // bullet
                int mobOid = p.ReadInt();
                p.ReadByte();  // 7
                p.ReadByte();  // critical
                int damage = p.ReadInt();
                SawAttack.TrySetResult((attacker, mobOid, damage));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task MeleeAttack_MirrorsToOtherPlayers()
    {
        var repo = new InMemoryCharacterRepository();
        Character attacker = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hitter", MapId = 100000000, Level = 5 });
        Character bystander = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Watcher", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 1000 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        // Bystander enters first.
        var watcher = new Watcher(bystander.Id);
        var watcherHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var wServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, watcherHandler);
        await using var wClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, watcher);
        _ = wServer.RunAsync(cts.Token);
        _ = wClient.RunAsync(cts.Token);

        int mobOid = fields.Get(100000000).Mobs[0].ObjectId;

        // Attacker enters and hits the mob for 100 (mob has 1000 HP, so it survives).
        var fighter = new Fighter(attacker.Id, new[] { 100 });
        var fighterHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, fighterHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, fighter);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (int who, int oid, int dmg) = await watcher.SawAttack.Task.WaitAsync(cts.Token);
        Assert.Equal(attacker.Id, who);
        Assert.Equal(mobOid, oid);
        Assert.Equal(100, dmg);
    }

    /// <summary>Reports a hit on entry and watches for another player's LP_UserHit mirror.</summary>
    private sealed class HitReporter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly bool _reportsHit;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opUserHit = ServerOps.Get("LP_UserHit");
        private bool _sent;

        public HitReporter(int characterId, bool reportsHit)
        {
            _characterId = characterId;
            _reportsHit = reportsHit;
        }

        public TaskCompletionSource<(int Victim, sbyte AttackIdx, int Damage, int MobTemplate, int Delta)> SawHit { get; } =
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
            if (opcode == _opSetField && _reportsHit && !_sent)
            {
                _sent = true;

                // JMS v186 CP_UserHit, mob-physical form (-1): the mob attacker block trails.
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);              // time
                w.WriteByte(unchecked((byte)(-1))); // nAttackIdx = mob physical
                w.WriteByte(0);             // nMagicElemAttr
                w.WriteInt(37);             // nDamage
                w.WriteInt(100100);         // mob template id
                w.WriteInt(1);              // mob object id
                w.WriteByte(0);             // nLeft
                w.WriteByte(0);             // nReflect
                w.WriteByte(0);             // unk
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opUserHit)
            {
                int victim = p.ReadInt();
                sbyte idx = (sbyte)p.ReadByte();
                int damage = p.ReadInt();
                int template = 0;
                template = p.ReadInt();
                p.ReadByte();               // left
                p.ReadByte();               // reflect
                p.ReadByte();               // guard
                int delta = p.ReadInt();
                SawHit.TrySetResult((victim, idx, damage, template, delta));
            }
        }
    }

    [Fact]
    public async Task UserHit_MirrorsToOtherPlayers()
    {
        // The oracle broadcasts LP_UserHit on every reported hit so onlookers see the damage
        // number and flinch (OnUserHit's unconditional broadcastMessage).
        var repo = new InMemoryCharacterRepository();
        Character victim = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Victim", MapId = 100000000, Hp = 500, MaxHp = 500 });
        Character bystander = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bystand", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var onlooker = new HitReporter(bystander.Id, reportsHit: false);
        var onlookerHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var oServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, onlookerHandler);
        await using var oClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, onlooker);
        _ = oServer.RunAsync(cts.Token);
        _ = oClient.RunAsync(cts.Token);

        var reporter = new HitReporter(victim.Id, reportsHit: true);
        var reporterHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var rServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, reporterHandler);
        await using var rClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, reporter);
        _ = rServer.RunAsync(cts.Token);
        _ = rClient.RunAsync(cts.Token);

        (int who, sbyte idx, int dmg, int template, int delta) = await onlooker.SawHit.Task.WaitAsync(cts.Token);
        Assert.Equal(victim.Id, who);
        Assert.Equal(-1, idx);           // mob physical
        Assert.Equal(37, dmg);
        Assert.Equal(100100, template);  // the attacker block rode along
        Assert.Equal(37, delta);
        Assert.Equal(500 - 37, victim.Hp);
    }

    private sealed class EffectWatcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEffect = ServerOps.Get(ServerOpcode.UserEffectRemote);

        public EffectWatcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int Cid, byte Type)> SawEffect { get; } =
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

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opEffect)
            {
                int cid = p.ReadInt();
                byte type = p.ReadByte();
                SawEffect.TrySetResult((cid, type));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task MeleeAttack_LevelUp_BroadcastsRemoteEffect()
    {
        var repo = new InMemoryCharacterRepository();
        Character attacker = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Newbie", MapId = 100000000, Level = 1, Exp = 0 });
        Character bystander = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Observer", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        // 20 exp >= ExpForLevel(1) = 15, so the kill dings the attacker to level 2.
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 20 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);

        using var cts = new CancellationTokenSource(Timeout);

        // Observer enters first and waits until it's actually in the field.
        var observer = new EffectWatcher(bystander.Id);
        var observerHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var oServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, observerHandler);
        await using var oClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, observer);
        _ = oServer.RunAsync(cts.Token);
        _ = oClient.RunAsync(cts.Token);
        await observer.Ready.Task.WaitAsync(cts.Token);

        // Attacker enters and kills the mob (40 + 40 = 80 > 50 HP).
        var fighter = new Fighter(attacker.Id, new[] { 40, 40 });
        var fighterHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, fighterHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, fighter);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (int cid, byte type) = await observer.SawEffect.Task.WaitAsync(cts.Token);
        Assert.Equal(attacker.Id, cid);
        Assert.Equal(0, type);         // UserEffect_LevelUp
        Assert.Equal(2, attacker.Level);
    }

    /// <summary>Alice: creates a party, invites a partner, then kills a mob once both are set.</summary>
    private sealed class PartyKiller : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _partnerName;
        private readonly int[] _damages;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private int _mobOid = -1;
        private bool _joined;
        private bool _invited;
        private bool _attacked;

        public PartyKiller(int characterId, string partnerName, int[] damages)
        {
            _characterId = characterId;
            _partnerName = partnerName;
            _damages = damages;
        }

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
                await session.SendAsync(Party(session, w => w.WriteByte(1))); // create party
            }
            else if (opcode == _opMobEnter)
            {
                _mobOid = p.ReadInt();
                await MaybeAttack(session);
            }
            else if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 8 && !_invited) // CreateDone -> invite the partner
                {
                    _invited = true;
                    await session.SendAsync(Party(session, w => { w.WriteByte(4); w.WriteString(_partnerName); }));
                }
                else if (op == 15) // partner joined
                {
                    _joined = true;
                    await MaybeAttack(session);
                }
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (_joined && _mobOid >= 0 && !_attacked)
            {
                _attacked = true;
                await session.SendAsync(BuildMeleeAttack(session, _mobOid, _damages));
            }
        }

        private static byte[] Party(MapleSession session, Action<PacketWriter> body)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            body(w);
            return w.ToArray();
        }
    }

    /// <summary>Bob: joins the party on invite and records exp granted to him.</summary>
    private sealed class PartyPartner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public PartyPartner(int characterId) => _characterId = characterId;

        public TaskCompletionSource<int> ExpGained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 4) // invite popup -> join
                {
                    int partyId = p.ReadInt();
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(3);
                    w.WriteInt(partyId);
                    await session.SendAsync(w.ToArray());
                }
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();          // unlock
                int mask = p.ReadInt();
                if (mask == 0)
                {
                    return;
                }

                int value = p.ReadInt();
                if ((mask & 0x10000) != 0) // Exp
                {
                    ExpGained.TrySetResult(value);
                }
            }
        }
    }

    [Fact]
    public async Task PartyKill_SharesExp_WithSameMapMembers()
    {
        var repo = new InMemoryCharacterRepository();
        // Level 20 so 300 exp doesn't trigger a level-up (threshold 20216), keeping exact exp assertable.
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Level = 20 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Level = 20 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 300 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);
        var parties = new PartyRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        // Bob online first so the invite finds him.
        var partner = new PartyPartner(bob.Id);
        var partnerHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, partnerHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, partner);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);

        // Alice creates the party, invites Bob, and kills the mob once he's joined.
        var killer = new PartyKiller(alice.Id, "Bob", new[] { 40, 40 });
        var killerHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, killerHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, killer);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int bobExp = await partner.ExpGained.Task.WaitAsync(cts.Token);

        // 300 exp, 2 same-map members: killer x2/(3) = 200, partner x0.3/(3) = 30.
        Assert.Equal(30, bobExp);
        Assert.Equal(30, bob.Exp);
        Assert.Equal(200, alice.Exp);
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

    [Fact]
    public void ParseShoot_ConsumesBulletFields_AndReadsTargets()
    {
        var config = ServerConfig.Jms186;
        var pw = new PacketWriter(encoding: config.CodePage);
        pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0x12);           // damagePerMob=2, mobCount=1
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteInt(0);               // skill
        pw.WriteInt(0); pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0); pw.WriteShort(0); pw.WriteByte(0); pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0); // attack time, dwID
        pw.WriteShort(5);             // ProperBulletPosition (shoot-only)
        pw.WriteShort(0);             // pnCashItemPos (shoot-only)
        pw.WriteByte(1);              // nShootRange0a (shoot-only)
        pw.WriteInt(4242);            // mob oid
        pw.WriteBytes(new byte[4]);
        pw.WriteBytes(new byte[8]);
        pw.WriteShort(0);
        pw.WriteInt(150); pw.WriteInt(150); // 2 damages
        pw.WriteInt(0);               // mob crc

        var reader = new PacketReader(pw.ToArray(), config.CodePage);
        AttackInfo info = AttackParser.ParseShoot(reader);

        AttackTarget t = Assert.Single(info.Targets);
        Assert.Equal(4242, t.MobObjectId);
        Assert.Equal(new[] { 150, 150 }, t.Damages);
        Assert.Equal(0, reader.Remaining); // the 5 bullet bytes were consumed
    }

    [Fact]
    public void ParseMagic_MatchesMeleeLayout_AndReadsSkill()
    {
        var config = ServerConfig.Jms186;
        var pw = new PacketWriter(encoding: config.CodePage);
        pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0x11);           // damagePerMob=1, mobCount=1
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteInt(2001005);         // magic skill id
        pw.WriteInt(0); pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteByte(0); pw.WriteShort(0); pw.WriteByte(0); pw.WriteByte(0);
        pw.WriteInt(0); pw.WriteInt(0);
        pw.WriteInt(999);             // mob oid
        pw.WriteBytes(new byte[4]);
        pw.WriteBytes(new byte[8]);
        pw.WriteShort(0);
        pw.WriteInt(500);             // damage
        pw.WriteInt(0);               // mob crc

        var reader = new PacketReader(pw.ToArray(), config.CodePage);
        AttackInfo info = AttackParser.ParseMagic(reader);

        AttackTarget t = Assert.Single(info.Targets);
        Assert.Equal(999, t.MobObjectId);
        Assert.Equal(500, t.TotalDamage);
        Assert.Equal(2001005, info.SkillId);
        Assert.Equal(0, reader.Remaining);
    }
}
