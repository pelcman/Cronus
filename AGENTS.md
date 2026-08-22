# AGENTS.md — Cronus Design, Roadmap, and Task Board

This file is the shared **project contract** for agents (and humans). The operational
guide (build steps, protocol spec, coding conventions) lives in [CLAUDE.md](CLAUDE.md);
this file holds the **rationale for design decisions, the roadmap, and the tasks (work and
improvements)**. The two files are complementary — if you find a contradiction, update both.

---

## 0. One-line summary

> Reimplement the core of a JMS v186 private server in C#/.NET, using Riremito/JMSv186
> (Java) as a side-by-side oracle. Get login working first, then widen in vertical slices.

### Final goal (set 2026-08-21)

> Grow Cronus from a localhost-only test server into one an **in-group** can actually play
> on: the operator opens a port on a **fixed public IP** and friends connect. Reach a state
> where **anyone with minimal knowledge can stand the server up by following the docs** —
> build, configure (host/IP, DB, WZ data), run, open the port, point the client, play.

This reframes the near-term work priorities:
1. **Deployability** — nothing hardcoded to `localhost`; a small, documented set of config
   knobs (public host/IP, ports, DB, WZ, start map). *(First step: the channel endpoint the
   login server hands to the client is configurable via `CRONUS_HOST`, not loopback.)*
2. **A reproducible setup guide** — `docs/SERVER_SETUP.md`: prerequisites, build, configure,
   run, firewall/port-forward, connect a client. Written for a non-expert.
3. **Minimal playability** — the core loop must actually hold up for several players
   (entry, movement, chat, mobs/drops, maps/portals, a few NPCs, leveling).

Scope note (see CLAUDE.md §8): this stays a **private, in-group** server for research /
educational / hobby use — not a public or commercial operation.

---

## 1. Design Philosophy (summary)

Details in CLAUDE.md §2. Key points only:

- **Always-Green** — build in vertical slices; keep it running at all times.
- **Differential Correctness** — judge correctness by byte-diff against the Java build.
- **YAGNI** — target JMS v186 only; drop multi-version support up front.
- **Layer separation** — network/crypto/serialization (hand-written) stays decoupled from
  game logic (ported from Java).
- **Reuse data assets** — wz_xml, SQL, opcodes, JS scripts reused unchanged.
- **Modern .NET** — rewrite on Pipelines instead of porting MINA.

## 2. Tech Choices and Rationale

| Item | Choice | Rationale / alternatives |
|---|---|---|
| Language / runtime | C# / .NET 10 (`net10.0`) | SDK installed. 8.0 LTS is an option; we prefer latest features |
| Networking | `System.IO.Pipelines` + async `Socket` | MINA (Java) concepts map ~1:1. Zero-copy oriented |
| DI | `Microsoft.Extensions.DependencyInjection` (Autofac if needed) | Maple2 uses Autofac; stick with the built-in until it's insufficient |
| Logging | `Microsoft.Extensions.Logging` (+ Serilog later) | Start standard, add Serilog when needed |
| DB | EF Core + Pomelo.EntityFrameworkCore.MySql | Reuse JMSv186's `sql/` schema |
| Scripting | Jint (C# JS engine) | Same "evaluate JS" model as Java's Nashorn; reuse existing assets |
| Testing | xUnit (or NUnit) | Maple2 uses NUnit; either works, xUnit for now |
| Game data | wz_xml (JMSv186's external repo) | Client-language-independent; reusable as-is |

### Things NOT adopted from the Maple2 layout
- **Physical World-server split over gRPC** — single process is enough initially; keep the
  structure splittable later.
- **DotRecast (navmesh) / Silk.NET / ImGui debug GUI** — MS2-specific, not needed for MS1.
- **MS2 crypto (Maple2.PacketLib)** — MS1/MS2 crypto differ; port JMS' AES-OFB instead.

---

## 3. Architecture Diagram (initial, single process)

```
                    ┌─────────────────────────────────────┐
   JMS v186 client  │            Cronus.Server.Host        │
   (+ EmuClient) ───┼──► Login (8484) ──┐                  │
                    │                    ├─► World registry │
                    │    Channel (7575)──┘   (in-process)   │
                    └──────────┬──────────────────┬─────────┘
                               │                  │
                     Cronus.Network        Cronus.Database (MySQL)
                    (crypto/codec/opcode)   Cronus.Data (wz_xml)
                               │
                        Cronus.Scripting (Jint)
```

Project dependencies: `Host → Server.* → {Network, Database, Data, Scripting} → Common`.
`Network` depends only on `Common` and knows nothing about game logic (layer separation).

---

## 4. Roadmap (milestones)

Each milestone means adding one "working vertical slice".

- [x] **M0: Scaffolding** — solution, projects, build/test, documentation.
- [x] **M1: Network core** — crypto (AES-OFB / shanda), PacketReader/Writer, opcode loader,
      Hello builder + 21 unit tests (all green). Golden-vector cross-check vs Java is
      deferred (see Backlog improvements).
- [x] **M2: Hello handshake wire-up** — Pipelines codec + `MapleSession` + `MapleListener`;
      accept TCP → send Hello → client syncs encryption. Verified via in-memory + loopback
      integration tests. Handshake byte-match vs a real client/RirePE still to be confirmed.
- [x] **M3: Login authentication** — `LoginHandler` parses `CP_CheckPassword`, `LoginService`
      auto-registers/authenticates, replies `LP_CheckPasswordResult` (exact JMS v186 success
      layout). End-to-end encrypted login test green. Runnable `Cronus.Server.Host` binds the
      login port.
- [x] **M4: World / channel select** — `LP_WorldInformation` list + terminator, recommend /
      latest world, `CP_SelectWorld` → `LP_SelectWorldResult` (empty character list),
      `CP_ViewAllChar`. `WorldRegistry` model. End-to-end flow test green.
- [x] **M5: Characters + persistence** — Character model/repositories (in-memory + EF Core),
      creation flow (name check + CP_CreateNewCharacter), JMS v186 GW_CharacterStat /
      AvatarLook serialization; characters appear on the selection screen.
- [x] **M6: Channel server + game entry** — `Cronus.Server.Channel`: CP_MigrateIn →
      LP_SetField with the full JMS v186 CharacterData blob (empty inventories/skills/
      quests). Host runs login + channel in one process. Byte-exact flow test.
- [x] **M7a: Field interaction** — Field/FieldRegistry (players per map, broadcast),
      mutual LP_UserEnterField on migrate-in, CP_UserMove relay (raw CMovePath verbatim +
      server-side position tracking), CP_UserChat broadcast, LP_UserLeaveField on
      disconnect. Multi-client tests over separate encrypted sessions.
- [x] **M7b: Map transfer** — CP_UserTransferFieldRequest → SetField map-change branch;
      direct map-id jumps and portal-by-name (resolved via wz map data).
- [x] **M7c: WZ data (`Cronus.Data`)** — WzData XML parser (wz_xml format), MapData/PortalData,
      WzMapProvider (on-demand load from a wz_xml tree via CRONUS_WZ) + InMemoryMapProvider.
      Portal graph now drives map transfer.
- [x] **M8a: NPC dialogue scripting (`Cronus.Scripting`)** — Jint engine running OdinMS-style
      JS scripts; NpcConversation (blocking-thread `cm`: sendNext/sendOk/askYesNo/askMenu/
      askText); CP_UserSelectNpc → run script, CP_UserScriptMessageAnswer → advance;
      LP_ScriptMessage encoder. Folder/dictionary script sources; sample script.
- [x] **M8b: NPC spawning** — wz `life` NPC placements parsed into MapData; FieldRegistry
      populates fields with NPCs (runtime object ids); LP_NpcEnterField on entry/transfer;
      CP_UserSelectNpc resolves object id → template id → script.
- [x] **M8c: Mob spawning** — wz `life` mob placements → MapData.Mobs; Field.Mobs (own
      object-id base); LP_MobEnterField (control-normal, 16-byte temp-stat mask, pre-BB
      CMob::Init) on entry.
- [x] **M8d: Script player API + GM commands** — NPC scripts get `player`
      (getName/getLevel/getMapId/getMeso/gainMeso, persisted via ICharacterRepository.Save);
      chat `!map/!meso/!pos/!help` GM commands.
- [x] **M8e: LP_StatChanged** — stat-change encoder (StatFlag + EncodeChangeStat, JMS v186
      pre-BB), so gainMeso / `!meso` update the client UI immediately.
- [x] **M8f: Character deletion** — CP_DeleteCharacter → LP_DeleteCharacterResult with
      account-ownership check; ICharacterRepository.Delete (in-memory + EF). Completes CRUD.
- [x] **M9a: Item/equipment serialization + persistence** — InventoryItem model; ItemEncoder
      (RawEncode + equip/bundle body, JMS v186); AvatarLook renders equipped items;
      CharacterData InventoryInfo encodes the equipped tab; new characters get the starter
      equips the client sends; items persist via an `items` table (EF Include/cascade).
- [x] **M9b: Melee combat core** — AttackParser (CP_UserMeleeAttack, JMS v186 variadic layout);
      FieldMob HP; damage application; LP_MobLeaveField on death. (The attack packet layout is
      ported from source — validate against a live capture; magic/shoot/body attacks, exp/drops,
      and the LP_UserMeleeAttack mirror to other players are follow-ups.)
- [x] **M9c: Mob wz stats + exp on kill** — `Cronus.Data` mob provider (Mob/{id}.img.xml →
      maxHP/exp/level); FieldMob HP/exp from wz; killing a mob grants exp (LP_StatChanged).
- [x] **M9d: Level-up** — pre-BB exp table (SharedExpTable), CharacterProgression (level-ups
      on exp gain: HP/MP/AP/SP gains, SP only for jobs, remainder carried); kill/script exp
      route through it and send the full LP_StatChanged.
- [x] **M9e: Meso drops** — killed mobs drop meso (LP_DropEnterField, placeholder amount);
      CP_DropPickUpRequest picks it up (LP_DropLeaveField + LP_StatChanged meso). Field drop pool.
- [x] **M9f: Attack mirror** — LP_UserMeleeAttack relays a player's swing + per-target damage
      (critical flag) to everyone else in the field (multiplayer combat is now visible).
- [x] **M9g: Quests (script-driven)** — Character started/completed quest state; NPC script
      API (cm/player hasQuest/isQuestDone/startQuest/completeQuest); quest records written into
      the CharacterData blob (survive relog visibly). Quest DB persistence + live LP_QuestRecord
      updates are follow-ups.
- [x] **M9h: Skills** — CP_UserSkillUpRequest spends SP to raise a skill (LP_StatChanged(Sp) +
      LP_ChangeSkillRecordResult); skill records written into CharacterData. Closes the
      level → SP → skill progression loop. (wz skill max-levels + DB persistence are follow-ups.)
- [x] **M9i: Mob control/movement** — one client is delegated a mob's AI
      (LP_MobChangeController on entry); CP_MobMove is acked (LP_MobCtrlAck) and relayed
      (LP_MobMove); control hands off to a remaining player on disconnect/transfer and clears
      on death; server tracks mob position from the path.
- [x] **M10: Combat depth & content** — item drops from drop tables (done in M10d).
  - [x] **M10a: quest/skill DB persistence** — `Skills`/`StartedQuests`/`CompletedQuests` now
        persist as JSON columns on the `characters` row (EF value converter + comparer), so
        progression survives a restart. Verified over SQLite (which, unlike the InMemory
        provider, applies the converters). *Note:* schema is still `EnsureCreated`, so an
        existing DB needs a recreate (or a real migration) to gain the new columns.
  - [x] **M10b: magic + ranged attacks** — `AttackParser` handles `CP_UserMagicAttack` (v186:
        byte-identical to melee) and `CP_UserShootAttack` (melee + bullet slot / cash-bullet /
        shoot-range fields), grounded in `ParseCUser_Attack`; a unified attack-mirror encoder
        emits `LP_UserMeleeAttack`/`Magic`/`Shoot`; the three handlers share damage application.
        Follow-ups: resolve+consume the bullet item (no USE-inventory model yet, sent as 0) and
        render skill effects (skill level sent as 0).
  - [x] **M10c: wz skill data (max level)** — `Cronus.Data` `WzSkillProvider`
        (`Skill/{skillId/10000:000}.img.xml` → `skill/{id}/level` count, name-padding tolerant,
        cached); `CP_UserSkillUpRequest` caps at the wz max level so SP can't over-level a skill.
        Wired via `CRONUS_WZ`; `NullSkillProvider` (no data → uncapped) when unset.
  - [x] **M10d: mob item drops (loot loop)** — killed mobs now roll their drop table
        (`Cronus.Data` `SqlDropProvider` parses the reference `drop_data.sql`; `CRONUS_DROPS`),
        ports `TacosReward.dropFromDatabase`: per-entry `rand(0..999) < chance` (bosses forced,
        EQUIP ×10), meso rows → meso piles, item rows → item stacks, fanned out (`DropRoller`,
        unit-tested). `FieldDrop` carries `ItemId`/`Quantity`; `ChannelPackets.DropEnterField`
        unifies meso/item (item adds the 8-byte `-1` expiration). Pickup adds the stack to the
        inventory (`Inventory.Add` + `LP_InventoryOperation` + `ShowItemGain`); newcomers see
        on-ground item drops. Mobs with no table fall back to a placeholder meso pile. Equip drops
        are deferred until the equip item body is client-verified. End-to-end tested (kill→field→
        pickup→inventory) plus packet golden + roll-arithmetic units.
  - [x] **M10e: item move / equip / unequip** — `CP_UserChangeSlotPositionRequest` lets a player
        drag items between slots and equip/unequip gear (ports
        `ReqCUser.OnUserChangeSlotPositionRequest`): `Inventory.Move` swaps slots (positive→negative
        = equip, negative→positive = unequip, both-positive = rearrange), relayed as an
        `LP_InventoryOperation` MOVE(2) op with the equipped-slot trailer byte; an equip change also
        broadcasts `LP_UserAvatarModified` so the field sees the new look. The equip item body was
        verified byte-identical to the reference (`DataGW_ItemSlotBase`), so equipped gear renders.
        Equip *stat bonuses* aren't recomputed yet (visual + slot only); dropping an item to the
        ground (dst==0) is not modelled yet. End-to-end tested (equip) + move-op goldens.
  - [x] **M10f: NPC shops** — vendor NPCs open a shop on click (auto-shop) and players buy/sell
        (`CP_UserShopRequest` → `LP_OpenShopDlg`/`LP_ShopResult`), ports `ReqCShopDlg`+`MapleShop`.
        `Cronus.Data` `SqlShopProvider` parses the reference `shops`+`shopitems` dump (`CRONUS_SHOPS`),
        scoped per INSERT block; `IItemProvider.GetPrice` reads the wz `info/price` for sell pricing.
        Buy debits meso + adds the item; sell removes the slot + credits the wz price; each replies a
        one-byte `LP_ShopResult`. `/shop <id>` opens a shop for testing. Token shops / rechargeables
        deferred. End-to-end buy test + packet goldens.
  - [x] **M10g: equip base stats + equip drops/buys + drop-to-ground** — `IItemProvider.GetEquipStats`
        loads an equip's wz base stats (`Character.wz/{folder}/{id:08}/info`, folder by search;
        sword 1302000 → 17 atk / 7 slots); `ChannelHandler.PopulateEquipStats` fills them onto each new
        equip before it's serialized, so **equip mob-drops and equip shop-buys are now live** with real
        stats (the earlier skips removed). Also item **drop-to-ground** (`CP_UserChangeSlotPositionRequest`
        dst==0): a player throws a bundle item onto the field (`Field.AddPlayerItemDrop`) for others to
        pick up. Deferred: equip stat bonuses on the wearer, token shops, equip drop-to-ground (a
        `FieldDrop` carries only the item id). Needs a `Character` junction in the wz root.
  - [x] **M10h: item effects — return scrolls + buff potions** — return/teleport scrolls (wz
        `spec/moveTo`) warp the player (999999999 = the map's return field); buff potions
        (`spec/pad|mad|pdd|mdd|acc|eva|speed|jump` + `time` ms) apply a temporary stat buff via
        `LP_TemporaryStatSet` (0x001E; 128-bit CTS mask reverse-word-order, per stat
        `[value:2][-itemId:4][durationMs:4]`, byte-verified against the reference example),
        `CP_UserStatChangeItemCancelRequest` (0x0042) clears it with `LP_TemporaryStatReset` (0x001F).
        `ConsumeSpec` gains moveTo + the buff fields. The client counts the duration down and expires
        the buff visually; a server-side expiry tick is a follow-up (buffs aren't used in server
        combat). End-to-end tests (scroll warp, buff drink) + packet goldens.
  - [x] **M10i: NPC storage / trunk** — account-scoped storage (`CP_UserTrunkRequest` 0x0036 →
        `LP_TrunkResult` 0x015A), ports `ReqCTrunkDlg`+`TacosStorage`: open (op 21, full dump with the
        8-byte DBCHAR mask + per-category items), deposit (flat 100-meso fee), withdraw, meso
        (>0 withdraw / <0 deposit), close. Item objects move between inventory and storage
        (`Inventory.Place`) so equip stats survive. `StorageRegistry` keyed by account id, persisted
        via `IStorageRepository` → the `storages` table (items as JSON) when `CRONUS_DB` is set.
        `/storage` opens it; NPC-script `sendStorage` is a follow-up. Deposit→withdraw round-trip
        test + packet goldens + repo round-trips.
  - [x] **M10j: persistent key settings** — the function-key map persists per character instead of
        resetting each entry (`CP_FuncKeyMappedModified` 0x008E → `LP_FuncKeyMappedInit` 0x017C, 94
        positional `[type:1][action:4]` slots, seeded from the reference's 42-entry default). Ports
        `ResCFuncKeyMappedMan`+`TacosKeyLayout`. `KeymapRegistry` keyed by character, persisted via
        `IKeymapRepository` → the `keymaps` table (bindings as JSON) when `CRONUS_DB` is set.
        Rebind-persists test + 94-slot golden + repo round-trips. *(Schema note: `EnsureCreated`
        doesn't migrate — an existing DB needs a recreate to gain the `storages`/`keymaps` tables.)*
  - [x] **M10l: quests** — the quest loop works through the client's quest UI
        (`CP_UserQuestRequest` 0x0069, actions accept=1/complete=2/resign=3 → `LP_UserQuestResult`
        0x00E3 op 8 + the MS_QuestRecordMessage journal update [1][questId:2][state:1]{started:
        progress str / completed: FILETIME / none: 0}). `Cronus.Data` `WzQuestProvider` parses
        `Quest/Check.img.xml`+`Act.img.xml` (start npc/lvmin; end mob/item requirements; act
        exp/money/pop/item rewards, negative count = taken). Mob kills advance the per-mob 3-digit
        progress string live (`UpdateQuestKillsAsync`, ports `updateQuestMobKills`); completion
        verifies kills+items, applies rewards, and plays the quest-complete effect (op 10 local +
        remote). Script `startQuest`/`completeQuest` now push the journal live too. Selectable
        (`prop`=-1) rewards use the complete request's `selection` index; script-quest actions (4/5)
        fall back to plain accept/complete until quest scripts land (`QuestCheck.Script` carries the
        wz start/endscript name for that). Deferred: lost-item action, running actual quest scripts,
        lottery (`prop`>0) rewards, prerequisite-quest/job checks. Accept/kill-progress/complete/
        selection e2e tests + wz parse test. Needs a `Quest` junction in the wz root.
  - [x] **M10k: buff skills** — casting a self-buff skill applies its temporary stat buff
        (`CP_UserSkillUseRequest` 0x0058 → `LP_SkillUseResult` 0x0023 + `LP_TemporaryStatSet` with
        reason = +skillId, `time`×1000 from wz), ports `ReqCUser.OnUserSkillUseRequest`+`TacosBuff`.
        `ISkillProvider.GetSkillEffect` loads the per-level effect; `SkillBuff.FromEffect` maps generic
        stats (reusing the item-buff bits) + signature buffs (Magic Guard/Dark Sight/Booster/Power
        Guard/Hyper Body via `x`/`y`). Deducts `mpCon` (`LP_StatChanged` MP); cancel clears the mask.
        **Party buffs**: a buff with a wz affect box (`lt`/`rb` = the reference's `isPartyBuff`
        marker — Haste/Rage/Hyper Body/…) also lands on party members in the same map. **Attack
        skills** deduct their `mpCon` at the learned level and mirror the real skill level to the
        field (`PrepareSkillAttackAsync`). Deferred: server-side skill-ownership validation,
        summons, the box-range check (map-wide for now), buff-expiry tick. Cast/attack tests +
        FromEffect goldens.

  - [x] **M10m: player-to-player trade** — the MiniRoom trade room (`CP_MiniRoom` 0x007F /
        `LP_MiniRoom` 0x015F, v186 protocol ops): create → invite → enter, staging items (removed
        immediately, equips move as objects so stats survive) and meso, exchange when both confirm,
        cancel/disconnect returns everything. `Trade.TryClose()` makes the simultaneous-confirm race
        exchange exactly once. In-room chat works. Mini-games / personal / hired shops not modelled.
        Full two-client e2e test.
  - [x] **M10n: rates & gameplay polish batch** — `CRONUS_RATE_EXP/_DROP/_MESO` multipliers;
        quest-locked drop rows (`drop_data.questid`) only fall for killers on the quest; quest
        lottery (`prop`>0) rewards pick one weighted row and accept gates on prerequisite quests +
        job list; skill casts use the server-side learned level (unlearned = ignored); shoot attacks
        resolve + consume the bullet from the USE slot (onlookers see the right projectile); NPC
        scripts gain `openShop(id)`/`openStorage()`; shop **recharge** refills stars/bullets to
        slotMax (+mastery ×10) at `round(unitPrice × missing)` (`WzData.GetDouble`,
        `IItemProvider.GetUnitPrice`).

  - [x] **M10o: buddy list** — friends work (`CP_FriendRequest` 0x0088 → `LP_FriendResult` 0x0039,
        `OpsFriend` values): add (target gets a hidden pending entry + the invite popup), accept
        (both sides turn visible), delete/decline, reload; the list block ports `CFriend::Reset`
        (`[id:4][name:13][hidden:1][channel:4][tag:17]` + in-shop ints). Login/logout pushes
        `FriendRes_Notify` channel updates to everyone listing the player. `Character.Buddies`
        persists as a JSON column. Adding is online-only for now (no by-name offline lookup yet);
        equips drop-to-ground also now carries the item instance (M10g follow-up closed). Two-client
        add→accept e2e test.
  - [x] **M10p: server-side buff expiry** — `BuffTracker` records every `LP_TemporaryStatSet` the
        handler grants (skill self/party buffs by +skillId, item buffs by −itemId) with its mask
        word[0] and duration; the 1 s `BuffExpiryService` tick removes lapsed buffs and pushes
        `LP_TemporaryStatReset` (ports the reference's per-effect `CancelEffectAction` schedule).
        Re-casts refresh, player cancels remove, logout clears. E2E cast→tick→reset test.
  - [x] **M10q: quest-script execution** — `CP_UserQuestRequest` actions 4/5 (opening/complete
        script) run `scripts/quest/{questId}.js` on the Jint engine (ports `TacosScriptQuest`):
        the script's `start()` / `end()` drives the dialog through the global `qm` (same
        conversation API as NPC scripts' `cm`) and falls back to the data-driven accept/complete
        when no script exists. Scripts also gained `player.gainItem/haveItem/itemQuantity` (live
        `LP_InventoryOperation`); NPC/portal/quest scripts share one `CreateScriptPlayer` surface.
        Sample script at `scripts/quest/1000.js`.
  - [x] **M10r: guilds** — `CP_GuildRequest` 0x0084 → `LP_GuildResult` 0x003B (the reference
        `GuildHandler` raw ops): create (HQ map 200000301 + 5m meso, or the free `/guildcreate`
        anywhere), invite→accept (pending-invite registry), leave (leader leaving disbands),
        expel, rank titles (62), member ranks (64), emblem (66; HQ + 15m), notice (68), member
        online/level updates (61/60), decline relay (`CP_GuildResult` → op 55). The guild info
        blob ports `getGuildInfo`+`addMemberData` (member rows derived live from the character
        store via `ICharacterRepository.ListByGuild`). `LP_UserEnterField`'s guild block now
        carries name+emblem so guilds render under characters. Guild/party/friend chat lands too
        (`CP_GroupMessage` 0x007C → `LP_GroupMessage` 0x0087, `OpsChatGroup` 0/1/2).
        `Character.GuildId/GuildRank` columns + a `guilds` table (rank titles as JSON);
        `GuildRegistry` tracks online members for broadcasts. Byte-layout tests + a two-client
        create→invite→join→disband e2e. Deferred: alliances, guild BBS, GP updates, capacity
        growth, the guild-ranking NPC board.
  - [x] **M10s: Omok & match-card mini-games** — MiniRoom types 1/2 (ports `MapleMiniGame` +
        `ResCMiniRoomBaseDlg`'s game builders, JMS v186 op values 47-66): create with the board
        item (4080000+n / 4080100), the balloon over the owner (`LP_UserMiniRoomBalloon`,
        `AnnounceBox/Interaction` layout, replayed to map entrants), join via balloon (password
        gate), ready→start, Omok stones with server-side five-in-a-row detection, match-card
        flips with pair matching and points, tie request/answer, give-up, "leave after game",
        turn timeout, kick, and results with win/tie/loss records stored as quest 122200/122210
        custom data ("L,T,W", persisted — the reference's own store). Owner leaving/disconnect
        closes the room. Rules unit tests + a full two-client create→join→ready→start→five-stones
        →result e2e.
  - [x] **M10t: personal shops (露店)** — MiniRoom type 4 (ports `MaplePlayerShop`): set up with a
        store-permit cash item in a Free Market room (910000001-22), stock listings (PSP_PutItem;
        stars/bullets list as one whole-stack bundle, equips shelve their instance so stats
        survive the sale), open for business via MRP_Balloon (the `AnnounceBox` balloon, type 4,
        replayed to map entrants), up to 3 browsing visitors, buys (PSP_BuyItem: bundles×price
        transfer meso both ways, `LP_InventoryOperation` + listing refresh to the whole room;
        a full sell-out closes with reason 14), owner reclaim (PSP_MoveItemToInventory), kick by
        name (PSP_Ban), and close-with-stock-return on owner leave/disconnect. Shop object ids are
        offset from mini-game ids (MRP_Enter carries only the id). Two-client stock→open→buy e2e.
        Deferred: hired merchants (type 5, the offline `EmployeePool` shop), blacklists.
  - [x] **M10u: equip scrolling** — `CP_UserUpgradeItemUseRequest` (ports the pre-BB scope of
        `scrollEquipWithId`): normal stat scrolls (wz `info/success`/`cursed`/`inc*` via the new
        `ScrollSpec`; scroll↔equip family gate ports `canScroll`), 10/60/100% behavior, clean
        slates 2049000-8 (slot restore below the wz `tuc`), chaos 20491xx (nonzero stats drift
        ±5/±10), white-scroll (2340000) protection, curse destruction. Result = scroll consume +
        equip re-add/remove in one `LP_InventoryOperation`, the field-wide
        `LP_UserItemUpgradeEffect` flash, and an avatar repaint when a worn equip changed. Rules
        unit tests + a scroll-the-worn-sword e2e. Deferred: Vega/tablets/potential (post-BB),
        the Legendary Spirit skill gate for scrolling unequipped items.
  - [x] **M10v: mob skills (working reference scope)** — the controller's `CP_MobMove`
        "next attack possible" flag now lets the server pick one of the mob's wz skills (ports
        `MobUsesSkill`: random pick, `MobSkill.img` cooldown `interval` + HP%% threshold gates,
        MP cost) and answer it in `LP_MobCtrlAck` so the client animates the cast. Effects match
        what actually works in the reference: self/area-heal 114 (`LP_MobDamaged` with a negative
        number) and summon 200 (spawns the skill's mobs at the caster via the new
        `Field.SpawnMob`, capped by the wz `limit`, handed to the controller). Mob skill lists
        parse from `Mob/{id}.img` `info/skill`; `MobSkill.img` loads lazily once. The reference's
        mob stat-buff branch is dead code there (stats map never broadcast) and pre-BB player
        diseases aren't in it either — both stay deferred rather than invent unverified layouts.
  - [x] **M10w: hired merchants (雇用商人)** — MiniRoom type 5 (ports `HiredMerchant` +
        `ResCEmployeePool`): create with a 503xxxx permit in an FM room → stocking view
        (`getHiredMerch`, shared ESP/PSP put-item path) → MRP_Balloon puts the employee NPC on
        the map (`LP_EmployeeEnterField`, balloon block; replayed to map entrants) and it keeps
        selling with the owner gone or offline. Visitor buys bank the taxed price on the merchant
        (`EntrustedStoreTax` brackets) and append to the owner's sold list; the merchant-branch
        listing refresh (PSP_Refresh + leading int) goes room-wide. The owner re-entering kicks
        browsers ("整理中" leave 17) into a management view (uptime, sold list, banked meso),
        can reclaim stock (ESP_MoveItemToInventory), and leaving either reopens (stock left) or
        packs up — remaining stock + banked meso pay out to the owner (online or off) and
        `LP_EmployeeLeaveField` clears the NPC. In-memory only for now (a restart closes all
        merchants; the `hiredmerch` table is the follow-up); withdraw-meso/blacklist ops are
        unhandled in the reference too and stay deferred. Full two-client lifecycle e2e.
  - [x] **M10x: polish & protocol-gap batch** — from a systematic diff of the reference's handled
        client packets vs ours: portable chairs (`LP_UserSetActivePortableChair` + the enter-field
        chair int), skill-macro persistence (`LP_MacroSysDataInit` rows), party invite
        accept/decline via `CP_PartyResult`, the client's own regen tick
        (`CP_UserChangeStatRequest`), charge-skill windups (`LP_UserSkillPrepare`), mob aggro
        takeover (`CP_MobApplyCtrl`), channel-change/cash-shop bounces
        (`LP_TransferChannelReqIgnored`), megaphones (`CP_UserConsumeCashItemUseRequest` 507
        family), token-currency shop entries (ReqItem/ReqItemQ), lost-quest-item restore,
        inventory gather/sort, live guild level updates, and the `hiredmerch` table so merchants
        survive restarts. Return scrolls now also route via `CP_UserPortalScrollUseRequest` (the
        opcode the real client uses).
  - [x] **M10y: pets** — pet items (500xxxx) now encode as `GW_ItemSlotPet` (type 3 + the v186
        pet body; previously a spawned pet would have corrupted the character-data blob), never
        stack, and persist name/level/closeness/fullness. Runtime: summon/dismiss
        (`LP_PetActivated` + `DataCPet.Init`), path relay (`CP_PetMove`), emotes/speech
        (`CP_PetAction`), feeding (fullness/closeness + live item refresh), the enter-field pet
        block, and portal-following via `LP_PetTransferField`. Single pet; pet level-ups and
        auto-loot are deferred.
  - [x] **M10z: combat/content/ops finale batch** — ad boards (黒板, cash 537x →
        `LP_UserADBoard` + enter-field replay); Crusader combo orbs (CTS bit 21 via the verified
        stat-set layout: cast = value 1, each landed swing charges to the skill's x, Panic/Coma
        consume; the reference never implemented orb tracking); the full signature-buff table
        (Invincible/Soul Arrow/WK charges/Dragon Blood/Holy Symbol/Meso Up/Shadow
        Partner/PickPocket/Meso Guard/Stance/Sharp Eyes/Mana Reflection/Maple Warrior) with the
        CTS masks widened to 64 bits (word[1] carries 32+); `/maxskills` (job-chain skill maxing
        via `ISkillProvider.GetSkillIds`); an **additive MySQL auto-migration** (missing
        tables/columns added in place at startup — no drop-and-recreate on upgrades); NPC content
        in Japanese for the whole progression loop (1st–4th job instructors incl. Kyrin's pirate
        one-stop, storage Goldmen, Victoria taxis) with `gainMaxHp/gainMaxMp` script APIs and a
        parse+`start()` gate over every shipped script; and the two long-standing intermittent
        test failures fixed (a Messenger test start race, and a real buff register-after-send
        ordering bug).
  - [x] **M10aa: reactors** — map reactor placements (Map.wz `reactor/{n}`), the Reactor.wz
        state machine (`event/0/state` transitions; a state with no event is terminal),
        `CP_ReactorHit` → `LP_ReactorChangeState` per stage → break (`LP_ReactorLeaveField`,
        `reactorTime` respawn on the world tick, `LP_ReactorEnterField` replayed to entrants),
        and `scripts/reactor/{id}.js` reward hooks with the full script player API. The wz
        junction gained Reactor.wz. Deferred: touch-reactors and item-drop-triggered reactors.
  - [x] **M10ab: stat growth + NPC coverage batch** (client-test feedback) — level-ups and
        AP-into-HP/MP now use the reference's job-scaled random growth tables (+growth passives,
        +INT/10 MP), `/level` raises run real level-ups, and `CRONUS_AUTO_REGISTER=0` turns off
        account auto-creation (allow-list mode for public hosting). NPC content: world-travel
        network on 15 station/guide NPCs (13 destinations), storage Goldmen in every Ossyria
        town, and a name-aware fallback greeting for every unscripted NPC (`WzNpcNameProvider`
        over String.wz — no NPC is silent anymore).
  - [x] **M10ac: beauty salons** — the `SM_ASKAVATAR` style-picker dialog end to end
        (`ScriptMessageAvatar` builder with the byte-layout test, `cm.askAvatar(text, styles)`
        returning the picked index, answer parsing was already in place) plus styling script
        APIs `player.getHair/getFace/getSkin/setHair/setFace/setSkin/isValidStyle`. Look
        changes persist, push `LP_StatChanged` (Skin/Face/Hair bits), and broadcast
        `LP_UserAvatarModified` to the field. A `WzStyleProvider` validates every id against
        the wz Character tree (invalid = refused, so a script can never crash a client with a
        nonexistent style), and 36 salon scripts cover hair style / hair color / face / eye
        color / skin NPCs in every town (Henesys, Kerning, Orbis, Ludi, Mu Lung, Ariant,
        Showa, and the CMS/visitor salons in the JMS data).

  - [x] **M10ad: player summons** — the full summon pool for 3rd/4th-job play:
        `LP_SummonedEnterField/LeaveField/Move/Attack/Hit` builders (byte-layout tested against
        `ResCSummonedPool` + `DataCSummoned.Init`, JMS v186 path), the pre-BB skill tables
        (puppets/octopus = stationary, hawks/dragon/phoenix/frostprey = fly, gaviota =
        fly-random + departs after striking, beholder/ifrit/elquines/bahamut/KoC = walk), and
        the CP handlers: move relays the raw CMovePath, attack validates damage through the
        99,999 cap and shares the kill flow (exp/quests/loot), puppet hits track HP and fade it
        at 0. Casting spawns it (recast replaces), cancel dismisses, expiry sweeps on the buff
        tick, entrants get standing summons replayed at their owner's position, and map
        change/logout despawns (the reference re-spawns across maps — documented
        simplification). End-to-end test: a priest's cast reaches the other client in the map.
        Also this batch: buddy capacity (+5 via NPC, entry-blob byte now per-character), the
        Fredrick remote merchant pack-up, and the wz map recovery rate driving sauna regen.
  - [x] **M10ae: Mystic Door** — the priest's town portal:
        `LP_TownPortalCreated/Removed/TownPortal` (byte-layouts per `ResCTownPortalPool`), a
        door pair standing in both maps (field side at the caster, town side on a free wz
        door-portal type-6 spot in the return town), `CP_EnterTownPortalRequest` warping to
        the other side (town side arrives on its door spot, field side on the nearest portal),
        expiry on the buff tick per map side, replay to entrants, and despawn on logout.
        Recast replaces the pair. Party-window door info is sent to the owner
        (`LP_TownPortal`) and to every party member (`PartyInfo_TownPortalChanged`, op 46 —
        the declared enum values are the live ones; the reference's `OpsParty.init()` remap is
        dead code, never called from `PacketOps`).
  - [x] **M10af: real multi-channel** — `CRONUS_CHANNELS` (default 2) channel servers on
        consecutive ports, each with its own `FieldRegistry` and world ticks; account-scoped
        registries (parties, guilds, messenger, storage, buddy, merchants) stay shared. The
        login routes character select to the channel picked at world select, and in-game
        channel change works: `CP_UserTransferChannelRequest` → persist → `LP_MigrateCommand`
        `[1][ip:4][port:2]` → the client reconnects and migrates into the target channel.
        Follow-ups landed: /find and whisper reach across channels (LR_OtherChannel = 3 with
        the 1-based channel number), buddy presence notifies every channel, and parties
        survive disconnects — a dropping member is marked offline on the roster (channel -2 in
        the window), re-attaches on relog/channel switch with a window refresh + HP sync, and
        the party dissolves only when the last member drops. The party window now shows each
        member's own channel. Remaining quirk: hired merchants are visible on every channel
        (the registry is shared).

  - [x] **M10ag: fidelity & QoL batch** — the death exp penalty now ports `playerDead`
        (1% in towns, `0.2/LUK + 0.05` in the field, archers 0.08, beginners exempt; was a
        flat −10%); pets loot drops for their owner (`CP_PetDropPickUpRequest` → the shared
        pickup path, LeaveType 5 with the pet slot); cooldown skills start the client timer
        (`LP_SkillCooltimeSet`, wz `cooltime`); and the Free Market is reachable: 21 town
        entrance portals (market01–18, marketJ00–02) remember the town via the new
        `player.rememberMap()/warpToRememberedMap()` script APIs (persisted) and market00
        walks back out. Event-only portals (`hidenPT_*`, unity) stay inert by design — their
        targets aren't in the data.

  - [x] **M10ah: Knights of Cygnus & Aran playable** — creation maps the pre-BB job type
        (0 = Cygnus → Noblesse 1000, 2 = Aran → Legend 2000; the oracle's `OpsNewCharacter`
        declared values), growth collapses to the explorer archetypes (`GrowthJob`), the
        beginner family (0/1000/2000) earns no SP and loses no exp on death, the five knight
        commanders + Lilin advance the branches (1x00→1x10→1x11 at 10/30/70; Aran
        2100→2110→2111→2112 at 10/30/70/120), the wz already carries all their skill data,
        and the travel network gained Ereve and Rien. Not modelled: the Ereve/Rien tutorial
        quest lines and Cygnus-specific passives in the growth bonuses.

  - [x] **M10ai: cash shop server** — a real cash-shop stage on its own port (the next after
        the channels): `LP_SetCashShop` (CharacterData + account name + empty sale/discount/
        best-item tables — the client's Etc.wz renders the catalog), balances
        (`LP_CashShopQueryCashResult`), the account locker (`CashItemRes_LoadLocker_Done`,
        55-byte `DataGW_CashItemInfo` entries), buying by commodity SN against the wz
        Commodity catalog (`WzCommodityProvider`), locker↔inventory moves (inventory cash
        items now carry their 8-byte unique id in `RawEncode`), destroy, and migrate-out back
        to the channel the player came from (`Character.LastChannel`). NX economy: `CRONUS_NX`
        tops every account up to an allowance floor on entry (accounts persist NexonPoint/
        MaplePoint/locker as columns + a JSON locker). Slot/trunk expansion and wishlists are
        declined like the reference; gifts/coupons are out of scope. End-to-end test: migrate
        in → buy a pet → move it into the inventory with its cash id.

- [x] **M11: World tick — mob respawn** — a server `MobRespawnService` (`PeriodicTimer`) brings
      dead mobs back after a delay (`FieldMob.RespawnAtTick`, set on kill), announcing
      `LP_MobEnterField` and handing control to a player present. Keeps hunting maps populated
      instead of emptying out. First use of the "timed world logic on a tick" pattern (CLAUDE.md
      §2). Uses the map spawn's `mobTime` (>0 = that many seconds, -1 = never/boss, 0 = default 7 s).
- [x] **M12: HP/MP regen tick** — idle players recover HP/MP on a `PlayerRegenService` tick;
      `FieldPlayer.LastActiveTick` gates it (moving/attacking resets the idle timer), the pure
      `PlayerRegen.Apply` rule tops HP/MP up toward max, and the change is pushed with
      `LP_StatChanged`. Simplified fixed regen for now; MapleStory's level/job scaling and
      sit/rest bonus are follow-ups.
- [x] **M13: Player takes mob damage** — `CP_UserHit` applies the client-reported hit damage to
      the player's HP (0 = dead, see M14) and pushes `LP_StatChanged`; taking a hit resets the
      regen idle timer. Combat is now two-sided — mobs threaten you, so HP/MP management (M12)
      matters. Follow-up: the hit-mirror so onlookers see the flinch.
- [x] **M14: Death & revive** — a hit that drops HP to 0 leaves the player dead (the client shows
      the tombstone); dismissing it sends `CP_UserTransferField`, which the server turns into a
      revive at the map's return town (`info/returnMap`, `MapData.ReviveMap`; or in place when the
      map has none) with full HP/MP. Closes the survival loop. Dying also costs exp
      (`CharacterProgression.ApplyDeathPenalty`: −10% of accumulated exp, no level-down; sent in
      the death `LP_StatChanged`). Follow-ups: level/map-scaled loss + town exemption.
- [x] **M15: Drop despawn** — the field world tick (`MobRespawnService`, now respawn + drop
      upkeep) fades drops left on the ground past their TTL (60 s) with `LP_DropLeaveField`
      (timeout). Keeps maps tidy and completes the drop lifecycle for when item drops land.
- [x] **M16: Emotes** — `CP_UserEmotion` relays a player's face emote to the rest of the field
      (`LP_UserEmotion`, grounded in `DataCUser.Emotion`) — a small social touch for an in-group
      server. Item-based expressions (id > 7) aren't inventory-checked yet.
- [x] **M17: Sitting / chairs** — `CP_UserSitRequest` (`[seatId:2]`, -1 = stand) seats the player
      and echoes `LP_UserSitResult` (`[sitting:1][seatId:2 if sitting]`). Seated players rest:
      `PlayerRegen` recovers 3× while `FieldPlayer.Seated`, and the regen tick skips the idle wait
      for them. Moving or attacking stands you back up (`Seated = false`). Map-chair objects and
      portable (cash) chair items aren't validated against inventory yet — any seat id is accepted.
- [x] **M18: Damage validation (server authority)** — `DamageValidator` bounds client-reported
      attack damage to what a legitimate pre-Big-Bang v186 client can produce: the hard per-line
      cap of 99,999 (`MaxDamagePerLine`), critical bit stripped, negatives floored.
      `ApplyAttackDamageAsync` now applies `ValidatedDamage(target)` (clamped-line sum) not trusting
      `target.TotalDamage`, and `CP_UserHit` clamps the reported hit too. Closes the "trusts
      client-reported damage" soft spot flagged in CLAUDE.md §2. Follow-ups: per-skill/weapon damage
      ceilings from wz, attack-rate limiting, and range checks vs. mob position.
- [x] **M19: Whisper & /find** — `CP_Whisper` routes a private message (WP_Whisper) or a location
      lookup (WP_Location) to a target found by name across the channel
      (`FieldRegistry.FindPlayerByName`, case-insensitive). The sender gets a delivered/not ack
      (`WhisperResult`), the recipient the message with sender name + channel (`WhisperReceive`),
      and /find reports the target's map or "not found" (`WhisperLocationResult`, ports
      `ReqCUser.OnWhisper` + `OpsLocationResult`). End-to-end tested through encrypted sessions.
      Cross-channel routing is out of scope (single-channel server). First cross-field social feature.
- [x] **M20: Level-up effect** — a kill that levels you up now broadcasts `LP_UserEffectRemote`
      (`UserEffect_LevelUp` = type 0: `[charId:4][type:1]`) to the rest of the field, so onlookers
      see the level-up animation. The local client plays its own from the level `LP_StatChanged`, so
      only the remote effect needs sending (ports `MapleCharacter.levelUp` →
      `ResCUserRemote.UserEffectRemote`). End-to-end tested (observer sees the attacker's ding).
- [x] **M21: Messenger** — the 3-person messenger window (`CP_Messenger` / `LP_Messenger`, ports
      `TacosMessenger` + `ReqCUIMessenger`). `Messenger` holds up to 3 slots and fans packets out to
      members across fields; `MessengerRegistry` (shared, injected like `FieldRegistry`) creates and
      looks them up. Ops: create/join (Enter → SelfEnterResult + Enter to peers), Invite (→
      InviteResult to members + Invite to the target), Chat, Leave; disconnect auto-leaves. The
      member avatar reuses the client-verified `WriteAvatarLook`, so the window renders real looks.
      End-to-end tested (invite → join → chat → leave through encrypted sessions). Out of scope:
      block-list (MSMP_Blocked), avatar refresh (MSMP_Avatar), and cross-channel migration.
- [x] **M22: Party** — the full party lifecycle (`CP_PartyRequest` / `LP_PartyResult`, ports
      `OnPartyRequest` + `OdinWorld.Party.updateParty`): create, invite, join, leave, disband (leader
      leaves), expel, and change-leader. `Party` holds up to 6 members with a leader and fans packets
      across fields; `PartyRegistry` (shared, injected) creates and looks them up; disconnect
      auto-leaves (leader → disband). The byte-critical 6-slot member-status block
      (`addPartyStatus`: ids, 13-byte names, jobs, levels, wire channels, leader, map ids, door
      blocks) is reproduced exactly and pinned with a golden-byte test (322-byte block, channel is
      1-based like the reference). End-to-end tested (create → invite → join → leave). Parties are
      in-memory / online-only. Follow-ups: party HP bars (`LP_UserHP` / `receivePartyMemberHP`) and
      leader reassignment on disconnect instead of disband.
- [x] **M23: Party exp sharing** — a kill now splits exp among the killer's party members on the
      same map instead of going wholly to the killer. `CharacterProgression.PartyExpShare` ports a
      simplified `MapleMonster.killedMob` split: pool = `baseExp / (members + 1)`, killer weight 2.0,
      others 0.3 — so a solo/no-party kill still yields full exp, and grouping trades a slice for
      giving partners a share. `GrantKillExpAsync` distributes to each same-map member (each gets
      their own `LP_StatChanged` and level-up effect). Exp is server-authoritative so no new packet /
      byte risk. Level/range and class/premium bonuses are not modelled. End-to-end tested (300-exp
      mob → killer 200, partner 30). Follow-up: party HP bars.
- [x] **M24: Party HP bars** — a party member's HP bar now shows on their same-map partners' screens
      (`LP_UserHP` = `[cid:4][hp:4][maxHp:4]`, ports `ResCUserRemote.UserHP`). Taking damage
      (`CP_UserHit`) and reviving push the change (`NotifyPartyOfMyHpAsync` →
      `updatePartyMemberHP`); joining exchanges current HP both ways (`SyncPartyHpAsync` →
      `updatePartyMemberHP` + `receivePartyMemberHP`). End-to-end tested (partner sees 500 → 380 on a
      120 hit). This is what lets a support build watch and heal the party. Follow-up: also push on the
      HP-regen tick (the regen service would need the party registry).
- [x] **M25: Party window liveness** — a member changing map or levelling up now rebroadcasts the
      party window (the silent-update op 7, reusing the golden-tested `PartyRefresh`), so everyone's
      window shows current maps and levels. `RefreshPartyWindowAsync` fires from `MovePlayerToMapAsync`
      and the level-up branch of `GrantExpToAsync` (ports the `SILENT_UPDATE` path). End-to-end tested
      (partner's window updates to the leader's new map after a warp).
- [x] **M26: Ground drops on entry** — a player entering (or warping into) a field now sees the meso
      drops already lying there, not just ones that drop after they arrive. `SpawnNpcsAsync` sends each
      existing `Field.Drops` with `LP_DropEnterField` using `NO_ANIMATION` (already-on-ground, no fall;
      ports `ResCDropPool.EnterType`). End-to-end tested (late arrival sees a pre-existing drop). Also
      bumped the integration-test timeouts (5 s → 15 s) so the suite stays green under parallel load.
- [x] **M27: Party HP bars on regen** — completes M24: the HP-regen tick now also pushes the
      recovered HP to same-map party members, so a partner's bar ticks up as they rest, not only on
      damage/revive. `PlayerRegenService` takes the shared `PartyRegistry` and calls
      `PushHpToPartyAsync` whenever a regen changed HP. End-to-end tested (partner sees 380 → 390 after
      a regen tick). The party HP-bar feature is now complete for all HP-change paths.
- [x] **M28: In-group GM commands** — the chat command set (lines starting with `!`) grew with
      genuinely useful, server-authoritative helpers: `!heal` (full HP/MP, also updates the party bar),
      `!warp <name>` (jump to an online player's map — how friends meet up), and `!players` / `!online`
      (list who's on). All are chat-triggered and touch no entry-blob bytes, so they carry no
      client-entry risk. End-to-end tested (`!warp` moves the caller into the target's field).
- [x] **M29: Dropping meso** — `CP_UserDropMoneyRequest` lets a player throw mesos on the ground for
      others to pick up (ports `ReqCUser.OnUserDropMoneyRequest`): bounds 10..50000 and affordability
      are enforced, the mesos are deducted, and a *player-owned* meso drop spawns at their feet
      (`Field.AddPlayerMesoDrop`; `LP_DropEnterField`'s origin byte flips to 0 for player drops). The
      existing pickup path then credits whoever grabs it — so friends can hand each other meso, the
      working currency, without an inventory system. End-to-end tested (250 meso Alice → Bob) plus the
      below-minimum reject. No entry-blob bytes touched.
- [x] **M30: AP allocation** — `CP_UserAbilityUpRequest` spends an ability point on a base stat
      (ports `ReqCUser.OnUserAbilityUpRequest`): the `CS_*` flag maps 1:1 onto `StatFlag`, so
      `CharacterProgression.SpendAbilityPoint` raises STR/DEX/INT/LUK by 1 (capped at 999) or MaxHP/
      MaxMP by a flat amount (job-scaled random is simplified away — server owns HP/MP), spends the AP,
      and replies `LP_StatChanged`. Rejected clicks (no AP / capped) send nothing, matching the client.
      Completes the level-up loop with the skill-up (`CP_UserSkillUpRequest`) already in place.
      End-to-end tested (STR 4→5, AP 3→2) plus the no-AP / capped / bad-flag units.
- [x] **M31: Auto-assign AP** — `CP_UserAbilityMassUpRequest` (the auto-assign button) spends all
      remaining AP across several base stats at once (ports `OnUserAbilityMassUpRequest`).
      `CharacterProgression.SpendAllAbilityPoints` validates each `[stat,points]` pair (STR/DEX/INT/LUK,
      non-negative) and that they sum to exactly the remaining AP, applies them, zeroes AP, and one
      `LP_StatChanged` carries all the raised stats. End-to-end tested (STR +3 / DEX +2 empties 5 AP)
      plus total-mismatch / non-base-stat rejects.
- [x] **M32: Fame / popularity** — `CP_UserGivePopularityRequest` lets a level-15+ player rate another
      online player's fame up or down (ports `ReqCUser.OnUserGivePopularityRequest`). The target gains
      or loses a point (clamped ±30000) and both sides are notified (`LP_GivePopularityResult`:
      Success to the giver with the new fame, Notify to the target, plus the target's `LP_StatChanged`).
      Guards: level 15 minimum, not self, target must be online on the same map, and one fame per target
      per session (a simplified stand-in for the once-per-day limit — a persisted fame log would make it
      real). End-to-end tested (Bob 0→1 fame, both notified, repeat rejected).
- [x] **M33: Periodic auto-save** — `CharacterAutoSaveService` is a server tick (every 2 min) that
      persists every online character, so an unexpected shutdown loses at most one interval's progress.
      Most stat changes already save on mutation (exp/meso/level/AP/map); this is the safety net that
      also flushes the drift saved lazily (notably regen'd HP/MP). Wired into the host's tick set. A
      pure `Tick()` (deduping characters across fields) is unit-tested. Rounds out durability for a
      real multi-hour in-group session.
- [x] **M34: Boss HP gauge** — damaging a boss now shows the whole field its HP gauge
      (`LP_FieldEffect` MobHPTag: `[flag=5][mobId:4][hp:4][maxHp:4][tagColor:1][tagBgColor:1]`, ports
      `ResCField.FieldEffect`). A mob counts as a boss when its wz `info/hpTagColor` is non-zero;
      `MobData.FromWz` now parses `hpTagColor`/`hpTagBgcolor`, `FieldMob` carries them (`IsBoss`), and
      `ApplyAttackDamageAsync` broadcasts the gauge on each hit to a tagged mob. Ordinary mobs (tag 0)
      are unaffected. End-to-end tested (boss 1000→750 gauge) plus the wz-parse and encoder units — a
      nice touch for an in-group boss run.
- [x] **M35: Loot feedback messages** — the satisfying floating text on a kill / pickup
      (`LP_Message`, ports `ResCWvsContext.Message`): "+N exp" (`IncExpMessage`, MS_IncEXPMessage=3,
      all bonus fields zeroed) fires from `GrantExpToAsync` for each recipient, and "+N mesos"
      (`IncMoneyMessage`, MS_IncMoneyMessage=6) fires on meso pickup. The JMS v186 message-type values
      are unambiguous (no OpsMessage remap applies to 148–193, so the enum defaults hold). End-to-end
      tested (kill shows the mob's 42 exp) plus both encoder layouts. Also capped the Channel test
      assembly's parallelism (`MaxParallelThreads = 4`) so the growing integration suite stays reliable
      under CPU load.
- [x] **M36: Character info window** — `CP_UserCharacterInfoRequest` (clicking another player) returns
      their info window `LP_CharacterInfo` (ports `ReqCUser.OnCharacterInfoRequest` +
      `ResCWvsContext.CharacterInfo`, JMS v186 path): id/level/job/fame, then the
      community/pet/mount/wishlist/monster-book/medal/chair blocks in their empty forms (Cronus models
      none of those yet; the guild "community" is `"-"`, which is what the reference always emits). The
      fixed layout is golden-tested exactly, and it's end-to-end tested (Alice sees Bob's lvl 55 / job
      412 / fame 21). Contained failure mode — it's a separate packet, not the entry blob.
- [x] **M37: Fame gain message** — completes M32: the target now also sees the "+1 / −1 fame" floating
      text (`LP_Message` / MS_IncPOPMessage=5, unambiguous for JMS v186) alongside their `LP_StatChanged`.
      `IncPopMessage(delta)` fires from `HandleGivePopularityAsync`. Encoder unit-tested.
- [x] **M38: NPC-script warp** — NPC scripts can now move the player: `player.warp(mapId)` /
      `player.warp(mapId, portal)` on the `INpcPlayer` surface (the field-mutating hook the interface
      always intended). `ChannelPlayer` runs it through a warp callback the handler wires to
      `MovePlayerToMapAsync`; it executes synchronously on the script thread (like the other `player`
      calls) — safe because the client is modal during a dialog, so no field-mutating packet is handled
      concurrently, and the transfer's operations are individually thread-safe. Unblocks town/dungeon
      NPCs. End-to-end tested: a `player.warp(...)` script moves the character across the wire.
- [x] **M39: NPC scripting API — stats & job** — widened the `player` surface for NPC scripts:
      getters (`getGender/getJob/getStr/getDex/getInt/getLuk/getFame/getAp/getSp`) and safe mutations
      (`gainAp`/`gainSp`/`gainFame` — additive, clamped; `setJob` — job advancement). Each mutation
      persists and pushes `LP_StatChanged`, the same verified pattern as `gainMeso`/`gainExp`/`heal`.
      This is what job-instructor, stat-reset, and fame NPCs are built from. End-to-end tested (a script
      setting job 200 + AP/SP/fame lands on the character).
- [x] **M40: Example NPC scripts** — shipped runnable starter content under `scripts/npc/` that shows
      the API off: `9010000.js` (menu + `gainMeso`), `9000021.js` (travel/heal via `heal`/`warp`), and
      `1012100.js` (a first-job instructor using `getJob`/`getLevel`/`setJob`/`gainSp`). The shipped
      scripts are loaded and run in a test (a level-5 beginner gets "come back at level 10"), so a typo
      or bad API call in them is caught in CI. A concrete starting point for a deployment's NPCs.
- [x] **M41: Scripted portals** — stepping on a portal that carries a wz `script` name now runs that
      script (`CP_UserPortalScriptRequest`, ports `ReqCUser.OnUserPortalScriptRequest`).
      `PortalData.Script`/`HasScript` are parsed from the map; `PortalScriptEngine` +
      `IPortalScriptSource` (folder `{name}.js` / dictionary) run it. A portal script has no blocking
      dialog — it just checks a condition and warps — so it runs in one shot off the packet loop with
      the `player` global (which now has `warp`). Host loads them from `CRONUS_SCRIPTS/portal/`.
      End-to-end tested: a scripted portal warps the character, a plain portal is a no-op; the wz
      `script` field is parse-tested. (Also fixed a `FakePlayer` test double left incomplete by M38/M39's
      wider `INpcPlayer`.)
- [x] **M42: More admin commands** — rounded out the operator toolkit (chat `!` commands, no client
      risk): `!job <n>`, `!ap <n>`, `!sp <n>`, `!fame <n>` (via a shared `SetStatAsync` that mutates,
      persists, and pushes the one changed stat), and `!save` (force-persist now). Handy for setting up
      test characters on an in-group server. `!job` is end-to-end tested; `!help` lists them all.

Reaching a "playable core" (combat, inventory, NPC) is a multi-week effort; full v186
parity is on the order of half a year.

**Done since:** the general inventory system (USE/ETC/SETUP/CASH tabs, CharacterData encoding,
live `LP_InventoryOperation`) and mob item drops both landed (M43/M44/M10d). The loot loop is
client-verifiable now.

**Blocked / needs a prerequisite:** *equip drops & equip shop-buys* are deferred until the equip
item body is client-verified — a byte error in the equip body would corrupt an
`LP_InventoryOperation`. Non-equip (bundle) drops use the client-verified encoding and are live.
Next core-loop features: **item move/equip** (`CP_UserChangeSlotPositionRequest`) and **NPC shops**
(buy/sell). MySQL persistence (`CRONUS_DB`) is the deployment prerequisite (in-memory wipes on
restart).

---

## 5. Backlog (work and improvements)

### Done (M1)
- [x] Solution/project scaffolding, docs, .gitignore, .gitattributes, LICENSE
- [x] `Cronus.Common`: `Region`, `ServerConfig` (JMS/186/MS932/region=3), `CodePage`
- [x] `Cronus.Network.Crypto.AesOfbCipher`: AES-OFB crypt / header write+read / check /
      IV advance (funnyShit) / multiplyBytes
- [x] `Cronus.Network.Crypto.ShandaCipher`: encrypt / decrypt (unused by JMS, ported)
- [x] `Cronus.Network.Packets.PacketWriter` / `PacketReader` (LE + MS932)
- [x] `Cronus.Network.Packets.OpcodeTable`: .properties loader (`@HEX`, decimal, `BASE ± offset`)
- [x] `Cronus.Network.Handshake`: plaintext Hello builder
- [x] opcode data `data/opcodes/JMS_v186_*.properties`
- [x] `Cronus.Network.Tests`: 21 tests — crypto round-trip (+multi-block, +IV lockstep),
      header write/read/check, shanda round-trip, opcode resolution, packet primitives, Hello

### Done (M2–M3)
- [x] `MapleSession` (Pipelines codec: holds IV pair, sends Hello, frames/dispatches)
- [x] `IPacketHandler` / `PacketHandlerBase` dispatch
- [x] `MapleListener` (TCP acceptor, one server session per connection)
- [x] `Cronus.Server.Login`: `LoginHandler` (CP_CheckPassword) + `LoginPackets`
      (LP_CheckPasswordResult, JMS v186 layout)
- [x] Account auth interim: `InMemoryAccountRepository` + `LoginService` (auto-register)
- [x] `Cronus.Server.Host`: runnable console host binding the login port (arg = port)

### Done (M4)
- [x] `WorldRegistry` / `GameWorld` / `GameChannel` model (default: 1 world, 2 channels)
- [x] `CP_WorldInfoRequest` → `LP_WorldInformation` list + terminator + recommend/latest
- [x] `CP_SelectWorld` → `LP_SelectWorldResult` (empty char list, JMS v186 layout)
- [x] `CP_ViewAllChar` → `LP_ViewAllCharResult` (count + empty success)
- [x] `CP_SelectCharacter` → `LP_SelectCharacterResult` migrate packet
- [x] `LoginState` per-connection state (account + selected world/channel)

### Done (M5a: DB + account persistence)
- [x] `Cronus.Domain` layer: `Account`, `IAccountRepository`, `InMemoryAccountRepository`
      moved here so infrastructure depends inward (ports & adapters). Login depends on Domain.
- [x] `Cronus.Database` (EF Core 9 + Pomelo/MySQL): `CronusDbContext` (accounts table),
      `DbAccountRepository`, `MySqlDatabase` helper (factory + EnsureCreated).
- [x] Host uses MySQL when `CRONUS_DB` is set, else falls back to in-memory (runs with zero
      external deps). Verified: starts and binds in both modes.
- [x] `Cronus.Database.Tests`: repository + LoginService-over-DB tests via EF Core InMemory.

### Next (M5b → characters, then channel)
- [ ] Character model + `DataGW_CharacterStat` / `DataAvatarLook` encoding (needed to show
      characters in `LP_SelectWorldResult` / `LP_ViewAllCharResult`)
- [ ] `CP_CheckDuplicatedID` / `CP_CreateNewCharacter` → create characters (persist via DB)
- [ ] Stand up a minimal channel server so `LP_SelectCharacterResult` migrate resolves
- [ ] Replace plaintext password with a real hash (BCrypt); consider async repository APIs
- [ ] EF Core migrations (replace EnsureCreated); reconcile with JMSv186 `sql/` schema
- [x] Verify against a live MySQL server — `CRONUS_DB` → MySQL 8.4: `EnsureCreated` builds the
      `accounts`/`characters`/`items` schema and the host logs "Connected to MySQL; …
      persistent." (2026-08-21). A full write/read-back integration test is still TODO.

### Improvements / tech debt (ongoing)
- [ ] **Add golden vectors**: run the Java build, capture handshake→login real bytes with
      RirePE, pin them in tests (currently round-trip only).
- [ ] Test AES-OFB block handling (0x5B0/0x5B4) with large payloads.
- [ ] Move to `Span<byte>`/`Memory<byte>`-centric APIs to cut allocations (naive first).
- [ ] Warn on undefined opcodes (@FFFF) at startup.
- [ ] Externalize ports / DB connection / data paths via appsettings.json.
- [x] Docker Compose (bundled MySQL) + Dockerfile — `docker compose up --build`.
      (Container build not run in the dev environment; standard .NET multi-stage pattern.)
- [x] CI (GitHub Actions: build + test on push/PR to main).

---

## 6. Repository Layout (current → target)

```
Cronus/
├─ CLAUDE.md / AGENTS.md / README.md / LICENSE / NOTICE.md
├─ .gitignore / .editorconfig / Directory.Build.props
├─ Cronus.sln
├─ src/
│  ├─ Cronus.Common/             (Region, ServerConfig, CodePage)
│  ├─ Cronus.Domain/             (Account, IAccountRepository, in-memory adapter)
│  ├─ Cronus.Network/            (Crypto/, Packets/, MapleSession, MapleListener)
│  ├─ Cronus.Database/           (EF Core + Pomelo/MySQL; CronusDbContext, DbAccountRepository)
│  ├─ Cronus.Data/               (later — wz_xml loader)
│  ├─ Cronus.Scripting/          (later — Jint)
│  ├─ Cronus.Server.Login/       (LoginHandler, LoginService, LoginPackets, World)
│  ├─ Cronus.Server.Channel/     (later)
│  └─ Cronus.Server.Host/        (runnable console host)
├─ tests/
│  ├─ Cronus.Network.Tests/
│  ├─ Cronus.Server.Login.Tests/
│  └─ Cronus.Database.Tests/
└─ data/
   └─ opcodes/                    (JMS_v186_*.properties)
```

---

## 7. Relationship to Upstream (Riremito)

- Cronus does **not** fork JMSv186; it is an independent C# rewrite.
- JMSv186 is kept on hand as the **reference oracle** — the primary source of truth for
  protocol, formulas, and opcode values.
- Upstream is actively developed (last push 2026-08), so consider tracking meaningful spec
  changes.
- Preserve credits and the license (AGPL-3.0).

## 7.5 Client testing & Riremito tooling

Real-client testing lives **outside** the Cronus repo, under the workspace root
(`c:\Users\chro\Desktop\MS1PrivSvr\`):

- `Client/MapleStory_v186/` — the real JMS **v186.1** client (`JMS_v186.1_L.exe`,
  EmuClient/localhost-patched). Complete WZ (2010-09), 4GB flag set. Runs on an NVIDIA
  GTX 1660 Ti. It has **active anti-cheat** — AhnLab HackShield (`aossdk.dll` +
  `tricod6_0_maple_md.dll` are **statically imported** by the exe; `v3hunt`/`bz32ex`/
  `suipre` alongside). The anti-cheat **rejects DLL-injection fixes** (dgVoodoo2's
  `d3d8.dll` triggered "不正なプログラムが検出されました") and **protects the game process
  from termination** (Stop-Process/taskkill fail). For this in-group private server the
  owner authorised removing the anti-cheat. NOTE: the anti-cheat DLLs are statically
  imported, so removal = stub DLLs or IAT patch, not deletion.
- `DevTools/` — all working tools + client custom libs (see `DevTools/README.md`):
  `procmon/`, `iGPUplz/` (built), `dgvoodoo/` (kept but unusable here — anti-cheat),
  `riremito/` (cloned Riremito repos), and `apply_*/revert_*/capture_*.bat`.

**Riremito's GitHub has a large toolbox for running/verifying these clients — reference it
whenever stuck; cloning into `DevTools/riremito/` is fine.** Key repos:
- `iGPUplz` — its JMS186 change (`CWzFileSystem::OpenDelayedArchive`/`OnGetSubItemProp`
  `6A 01`→`6A 02`, "gfx fix (pre-bb)") is **the confirmed fix** for the game-entry crash
  (client opens the field's delayed WZ archive on entry; default mode fails here →
  `STG_E_FILENOTFOUND 0x80030002` → crash). **The iGPUplz NameSpace.dll *proxy* (built from
  source, `DevTools/riremito/iGPUplz/build.bat`) crashes THIS client at startup in PCOM.DLL**
  (its LoadLibrary-in-DllMain is incompatible), so instead apply the same change **directly
  to `NameSpace.dll` on disk** (offsets 0xE923/0xEDC6) via `DevTools/wzpatch_namespace.py
  apply` — client entering the game confirmed. (Swap a locked NameSpace.dll by renaming it
  first, then copying — the anti-cheat protects the process from taskkill.)
- `EmuClient` / `LocalHost` / `RunEmu` / `Taco112` / `Teresa232` — localhost redirectors.
- `RirePE` — packet editor/logger (differential packet verification vs Cronus).
- `TeresaBeta` — "Remove BlackCipher/BlackCall" (anti-cheat removal, newer clients).
- `wz_xml` / `jms_wz` — WZ data as HaRepacker XML (v186 map data was pulled from here).
- `HaRepackerJ`, `WzMonitor`, `Injector`, `tools` (build deps).

**Key result:** the game-entry crash is **client-side** (DX8/WZ on a modern GPU), not a
Cronus bug — proven because the reference server with full real WZ crashes the same client
identically. Server correctness is verified independently (SetField byte-matches the
reference; entry sequence ported). Details: `Cronus/docs/GAME_ENTRY_DIAGNOSIS.md`,
root `CLIENT_RENDERING_FIX.md`.

## 8. Agent Operating Rules

- **Commit frequently and push** (per meaningful unit). Feel free to use a working branch.
- Any change touching byte boundaries must be committed **together with its test**.
- Do not fill unknown protocol behavior by guessing; ground it in the relevant JMSv186
  code. Mark ungrounded spots as TODO and add them to the Backlog.
- Avoid destructive / irreversible operations (force push, history rewrite, deletion).
- Update this file and CLAUDE.md whenever a design decision changes (don't let docs rot).
- Write documentation in English.
