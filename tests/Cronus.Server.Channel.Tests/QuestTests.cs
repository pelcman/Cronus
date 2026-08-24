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

public class QuestTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void WzQuestProvider_ParsesCheckAndAct()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-quest-wz-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Quest"));
        File.WriteAllText(Path.Combine(root, "Quest", "Check.img.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <imgdir name="Check.img">
              <imgdir name="1000">
                <imgdir name="0"><int name="npc" value="9000021"/><int name="lvmin" value="5"/></imgdir>
                <imgdir name="1">
                  <int name="npc" value="9000021"/>
                  <imgdir name="mob">
                    <imgdir name="0"><int name="id" value="100100"/><int name="count" value="2"/></imgdir>
                  </imgdir>
                  <imgdir name="item">
                    <imgdir name="0"><int name="id" value="4000019"/><int name="count" value="3"/></imgdir>
                  </imgdir>
                </imgdir>
              </imgdir>
            </imgdir>
            """);
        File.WriteAllText(Path.Combine(root, "Quest", "Act.img.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <imgdir name="Act.img">
              <imgdir name="1000">
                <imgdir name="0"></imgdir>
                <imgdir name="1">
                  <int name="exp" value="300"/><int name="money" value="500"/>
                  <imgdir name="item">
                    <imgdir name="0"><int name="id" value="2000000"/><int name="count" value="5"/></imgdir>
                    <imgdir name="1"><int name="id" value="4000019"/><int name="count" value="-3"/></imgdir>
                    <imgdir name="2"><int name="id" value="1092000"/><int name="count" value="1"/><int name="prop" value="-1"/></imgdir>
                  </imgdir>
                </imgdir>
              </imgdir>
            </imgdir>
            """);

        QuestData? quest = new WzQuestProvider(root).GetQuest(1000);

        Assert.NotNull(quest);
        Assert.Equal(5, quest!.StartCheck!.LevelMin);
        QuestMobEntry mob = Assert.Single(quest.EndCheck!.Mobs);
        Assert.Equal(100100, mob.MobId);
        Assert.Equal(2, mob.Count);
        Assert.Equal(3, Assert.Single(quest.EndCheck.Items).Count);
        Assert.Equal(300, quest.EndAct!.Exp);
        Assert.Equal(500, quest.EndAct.Money);
        Assert.Equal(3, quest.EndAct.Items.Count);
        Assert.Equal(-3, quest.EndAct.Items[1].Count);        // taken away
        Assert.Equal(-1, quest.EndAct.Items[2].Prop);         // selectable reward marked
        Assert.Null(new WzQuestProvider(root).GetQuest(9999)); // unknown quest
    }

    /// <summary>Sends a quest request on entry and captures quest-record messages.</summary>
    private sealed class QuestClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly byte _action;
        private readonly int _questId;
        private readonly int _selection;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);
        private bool _sent;

        public QuestClient(int characterId, byte action, int questId, int selection = -1)
        {
            _characterId = characterId;
            _action = action;
            _questId = questId;
            _selection = selection;
        }

        public TaskCompletionSource<(byte State, string Progress)> Record { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InventoryFull { get; } =
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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserQuestRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(_action);
                w.WriteShort((short)_questId);
                w.WriteInt(9000021);   // npc id
                if (_action == 2)
                {
                    w.WriteInt(_selection);
                }

                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opMessage)
            {
                byte messageType = p.ReadByte();
                if (messageType == 0 && p.ReadByte() == 0xFF)
                {
                    InventoryFull.TrySetResult(); // the "inventory is full" toast
                    return;
                }

                if (messageType != 1)
                {
                    return; // not a quest record
                }

                int questId = p.ReadShort() & 0xFFFF;
                if (questId != _questId)
                {
                    return;
                }

                byte state = p.ReadByte();
                string progress = state == 1 ? p.ReadString() : string.Empty;
                Record.TrySetResult((state, progress));
            }
        }
    }

    private static QuestData KillQuest(int questId) => new()
    {
        QuestId = questId,
        StartCheck = new QuestCheck { Npc = 9000021, LevelMin = 1 },
        EndCheck = new QuestCheck
        {
            Npc = 9000021,
            Mobs = new[] { new QuestMobEntry(100100, 2) },
        },
        EndAct = new QuestAct { Exp = 300, Money = 500 },
    };

    [Fact]
    public async Task AcceptQuest_StartsRecordWithZeroedProgress()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Novice", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { KillQuest(1000) });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 1, questId: 1000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte state, string progress) = await client.Record.Task.WaitAsync(cts.Token);

        Assert.Equal(1, state);            // started
        Assert.Equal("000", progress);     // one required mob, zero kills
        Assert.Equal("000", hero.StartedQuests[1000]);
    }

    [Fact]
    public async Task CompleteQuest_AppliesRewards()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Finisher", MapId = 100000000, Meso = 0,
            Level = 30, // high enough that the 300-exp reward doesn't level (keeps the assert exact)
        });
        hero.StartedQuests[1000] = "002"; // kills already done
        repo.Save(hero);

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { KillQuest(1000) });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 2, questId: 1000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte state, _) = await client.Record.Task.WaitAsync(cts.Token);

        Assert.Equal(2, state);                                // completed record
        Assert.False(hero.StartedQuests.ContainsKey(1000));
        Assert.True(hero.CompletedQuests.ContainsKey(1000));
        Assert.Equal(500, hero.Meso);                          // money reward
        Assert.Equal(300, hero.Exp);                           // exp reward (no level-up at 30)
    }

    [Fact]
    public async Task CompleteQuest_GivesOnlyTheSelectedReward()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Chooser", MapId = 100000000, Level = 30,
        });
        hero.StartedQuests[1001] = string.Empty;
        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1001,
            EndCheck = new QuestCheck { Npc = 9000021 },
            EndAct = new QuestAct
            {
                Items = new[]
                {
                    new QuestItemEntry(2000000, 5),            // plain reward: always given
                    new QuestItemEntry(1302000, 1, Prop: -1),  // selectable 0
                    new QuestItemEntry(1302001, 1, Prop: -1),  // selectable 1  <- picked
                },
            },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 2, questId: 1001, selection: 1);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte state, _) = await client.Record.Task.WaitAsync(cts.Token);

        Assert.Equal(2, state);
        Assert.Single(hero.EquippedItems, i => i.ItemId == 2000000);       // plain reward given
        Assert.Single(hero.EquippedItems, i => i.ItemId == 1302001);       // the chosen sword
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 1302000); // the other option not given
    }

    [Fact]
    public void WzQuestProvider_ParsesNextQuestSkillsAndQuestStates()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-quest-wz-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Quest"));
        File.WriteAllText(Path.Combine(root, "Quest", "Check.img.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <imgdir name="Check.img"><imgdir name="1000"><imgdir name="0"></imgdir><imgdir name="1"></imgdir></imgdir></imgdir>
            """);
        File.WriteAllText(Path.Combine(root, "Quest", "Act.img.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <imgdir name="Act.img">
              <imgdir name="1000">
                <imgdir name="0"></imgdir>
                <imgdir name="1">
                  <int name="nextQuest" value="1001"/>
                  <imgdir name="skill">
                    <imgdir name="0">
                      <int name="id" value="9001"/><int name="skillLevel" value="1"/><int name="masterLevel" value="1"/>
                      <imgdir name="job"><int name="0" value="100"/><int name="1" value="110"/></imgdir>
                    </imgdir>
                  </imgdir>
                  <imgdir name="quest">
                    <imgdir name="0"><int name="id" value="2100"/><int name="state" value="2"/></imgdir>
                  </imgdir>
                </imgdir>
              </imgdir>
            </imgdir>
            """);

        QuestData? quest = new WzQuestProvider(root).GetQuest(1000);

        Assert.NotNull(quest);
        Assert.Equal(1001, quest!.EndAct!.NextQuest);
        QuestSkillEntry skill = Assert.Single(quest.EndAct.Skills);
        Assert.Equal(9001, skill.SkillId);
        Assert.Equal(1, skill.SkillLevel);
        Assert.Equal(new[] { 100, 110 }, skill.Jobs);
        QuestPrereq stateChange = Assert.Single(quest.EndAct.QuestStates);
        Assert.Equal(2100, stateChange.QuestId);
        Assert.Equal(2, stateChange.State);
    }

    /// <summary>Completes a quest and captures LP_UserQuestResult (op, quest, npc, nextQuest).</summary>
    private sealed class QuestResultClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _questId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opQuestResult = ServerOps.Get("LP_UserQuestResult");
        private bool _sent;

        public QuestResultClient(int characterId, int questId)
        {
            _characterId = characterId;
            _questId = questId;
        }

        public TaskCompletionSource<(byte Op, int QuestId, short NextQuest)> Result { get; } =
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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserQuestRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(2);                // complete
                w.WriteShort((short)_questId);
                w.WriteInt(9000021);
                w.WriteInt(-1);                // no selection
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opQuestResult)
            {
                byte op = p.ReadByte();
                int quest = p.ReadShort() & 0xFFFF;
                p.ReadInt();                   // npc
                short next = p.ReadShort();
                Result.TrySetResult((op, quest, next));
            }
            else if (opcode == ServerOps.Get(ServerOpcode.Message))
            {
                if (p.ReadByte() == 1 && (p.ReadShort() & 0xFFFF) == _questId && p.ReadByte() == 2)
                {
                    CompletedRecord.TrySetResult();
                }
            }
        }

        /// <summary>Set when the completed (state 2) quest record for the quest arrives — the
        /// completion signal for quests without a nextQuest, which send no LP_UserQuestResult.</summary>
        public TaskCompletionSource CompletedRecord { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Fact]
    public async Task CompleteQuest_ChainsNextQuest_GrantsSkills_AndSetsQuestStates()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Chained", MapId = 100000000, Level = 30, Job = 100,
        });
        hero.StartedQuests[1000] = string.Empty;
        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1000,
            EndCheck = new QuestCheck { Npc = 9000021 },
            EndAct = new QuestAct
            {
                NextQuest = 1001,
                Skills = new[]
                {
                    new QuestSkillEntry(1000, 1, 0, new[] { 100, 110 }),   // job matches (100)
                    new QuestSkillEntry(2000000, 1, 0, new[] { 200 }),     // job filter excludes
                },
                QuestStates = new[] { new QuestPrereq(2100, 2) },
            },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestResultClient(hero.Id, questId: 1000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte op, int questId, short nextQuest) = await client.Result.Task.WaitAsync(cts.Token);

        Assert.Equal(8, op);                                   // QuestRes_Act_Success
        Assert.Equal(1000, questId);
        Assert.Equal(1001, nextQuest);                         // the chain the client auto-opens
        Assert.Equal(1, hero.Skills[1000]);                    // job-matched skill granted
        Assert.False(hero.Skills.ContainsKey(2000000));        // filtered out by job
        Assert.True(hero.CompletedQuests.ContainsKey(2100));   // quest-state act applied
    }

    [Fact]
    public async Task CompleteQuest_GrantsSpWithJobFilter()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "SpGains", MapId = 100000000, Level = 30, Job = 110, Sp = 2,
        });
        hero.StartedQuests[1000] = string.Empty;
        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1000,
            EndCheck = new QuestCheck { Npc = 9000021 },
            EndAct = new QuestAct
            {
                SpGrants = new[]
                {
                    new QuestSpEntry(3, new[] { 100 }),   // job 110 >= 100 -> granted
                    new QuestSpEntry(5, new[] { 2200 }),  // Evan-only -> filtered out
                    new QuestSpEntry(1, Array.Empty<int>()), // unfiltered -> granted
                },
            },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestResultClient(hero.Id, questId: 1000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // No nextQuest → completion answers with the record alone (no LP_UserQuestResult:
        // the oracle only sends that on completion from the nextQuest act, and an unsolicited
        // one crashes the live client's NPC dialog).
        await client.CompletedRecord.Task.WaitAsync(cts.Token);

        Assert.False(client.Result.Task.IsCompleted);
        Assert.Equal(2 + 3 + 1, hero.Sp);   // both matching grants applied, the Evan row skipped
    }

    [Fact]
    public async Task IntervalQuest_ReacceptableAfterTheInterval()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Repeater", MapId = 100000000, Level = 30,
        });
        // Completed 2 hours ago (FILETIME ticks are 100ns).
        hero.CompletedQuests[1000] = DateTime.UtcNow.AddHours(-2).ToFileTimeUtc();
        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1000,
            StartCheck = new QuestCheck { Npc = 9000021, IntervalMinutes = 60 }, // repeatable hourly
            EndCheck = new QuestCheck { Npc = 9000021 },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 1, questId: 1000);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte state, _) = await client.Record.Task.WaitAsync(cts.Token);

        Assert.Equal(1, state);                                  // started again
        Assert.True(hero.StartedQuests.ContainsKey(1000));
        Assert.False(hero.CompletedQuests.ContainsKey(1000));    // completion cleared for the rerun
    }

    /// <summary>Kills the mob once and captures the resulting quest progress update.</summary>
    private sealed class QuestHunter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);
        private bool _setField;
        private int _mobOid = -1;
        private bool _attacked;

        public QuestHunter(int characterId) => _characterId = characterId;

        public TaskCompletionSource<string> Progress { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            else if (opcode == _opMessage)
            {
                if (p.ReadByte() != 1)
                {
                    return;
                }

                p.ReadShort();                 // quest id
                if (p.ReadByte() == 1)         // started/progress record
                {
                    Progress.TrySetResult(p.ReadString());
                }
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (!_setField || _mobOid < 0 || _attacked)
            {
                return;
            }

            _attacked = true;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteByte(0);
            w.WriteInt(0); w.WriteInt(0);
            w.WriteByte(0x11);                 // 1 target, 1 hit
            w.WriteInt(0); w.WriteInt(0);
            w.WriteInt(0);                     // no skill
            w.WriteInt(0); w.WriteInt(0); w.WriteInt(0);
            w.WriteByte(0);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteByte(0);
            w.WriteInt(0);
            w.WriteInt(0);
            w.WriteInt(_mobOid);
            w.WriteBytes(new byte[4]);
            w.WriteBytes(new byte[8]);
            w.WriteShort(0);
            w.WriteInt(99);                    // lethal (mob has 50 HP)
            w.WriteInt(0);                     // mob crc
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task KillingQuestMob_AdvancesTheProgressRecord()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hunter", MapId = 100000000 });
        hero.StartedQuests[1000] = "000"; // quest already accepted
        repo.Save(hero);

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var mobData = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 5 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);
        var quests = new InMemoryQuestProvider(new[] { KillQuest(1000) });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestHunter(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        string progress = await client.Progress.Task.WaitAsync(cts.Token);

        Assert.Equal("001", progress);                 // one of two kills recorded
        Assert.Equal("001", hero.StartedQuests[1000]);
    }

    [Fact]
    public async Task CompleteQuest_FiltersRewardRowsByJobAndGender()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Fighter", MapId = 100000000, Level = 30,
            Job = 111, Gender = 0, // a male crusader
        });
        hero.StartedQuests[1002] = string.Empty;
        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1002,
            EndCheck = new QuestCheck { Npc = 9000021 },
            EndAct = new QuestAct
            {
                Items = new[]
                {
                    new QuestItemEntry(1072000, 1, Job: 0x2),   // warrior family  <- given (111/100 == 100/100)
                    new QuestItemEntry(1072001, 1, Job: 0x4),   // magician family    (filtered out)
                    new QuestItemEntry(1050000, 1, Gender: 0),  // male            <- given
                    new QuestItemEntry(1051000, 1, Gender: 1),  // female             (filtered out)
                    new QuestItemEntry(2000000, 3, Gender: 2),  // both            <- given
                },
            },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 2, questId: 1002);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (byte state, _) = await client.Record.Task.WaitAsync(cts.Token);

        Assert.Equal(2, state);
        Assert.Single(hero.EquippedItems, i => i.ItemId == 1072000);          // job-matched
        Assert.Single(hero.EquippedItems, i => i.ItemId == 1050000);          // gender-matched
        Assert.Single(hero.EquippedItems, i => i.ItemId == 2000000);          // gender 2 = both
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 1072001);  // other job's row
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 1051000);  // other gender's row
    }

    [Fact]
    public async Task CompleteQuest_WithFullInventory_KeepsTheQuestAndRewards()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Packrat", MapId = 100000000, Level = 30,
        });
        hero.StartedQuests[1003] = string.Empty;

        // Fill every equip-tab slot so the reward equip cannot fit.
        for (short slot = 1; slot <= GameConstants.InventorySlotsPerTab; slot++)
        {
            hero.EquippedItems.Add(new InventoryItem { ItemId = 1302000, Position = slot });
        }

        repo.Save(hero);

        var quest = new QuestData
        {
            QuestId = 1003,
            EndCheck = new QuestCheck { Npc = 9000021 },
            EndAct = new QuestAct
            {
                Exp = 300,
                Items = new[] { new QuestItemEntry(1072000, 1) }, // an equip that has no room
            },
        };

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var quests = new InMemoryQuestProvider(new[] { quest });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new QuestClient(hero.Id, action: 2, questId: 1003);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, quests: quests);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // The full-inventory notice arrives instead of a completion record.
        await client.InventoryFull.Task.WaitAsync(cts.Token);

        Assert.True(hero.StartedQuests.ContainsKey(1003));                    // still open — retryable
        Assert.False(hero.CompletedQuests.ContainsKey(1003));
        Assert.Equal(0, hero.Exp);                                            // nothing was applied
        Assert.DoesNotContain(hero.EquippedItems, i => i.ItemId == 1072000);  // reward not destroyed
    }
}
