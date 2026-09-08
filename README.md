# Cronus

[![CI](https://github.com/pelcman/Cronus/actions/workflows/ci.yml/badge.svg)](https://github.com/pelcman/Cronus/actions/workflows/ci.yml)

An open-source private-server emulator for the Japanese version of MapleStory
(JMS / JapanMS **v186**). The network core and game logic are implemented in
**C# / .NET 10**.

> ⚠️ **Note**: This project is strictly for local, research, and educational use.
> MapleStory is Nexon's intellectual property; we do not operate public or commercial
> private servers.

## What this is

- Target client: **JMS v186** (the last stable line before Big Bang)
- Reference implementation (side-by-side oracle):
  [Riremito/JMSv186](https://github.com/Riremito/JMSv186) (Java)
- Client-side tools: [EmuClient](https://github.com/Riremito/EmuClient) /
  [RirePE](https://github.com/Riremito/RirePE) (reused as-is)
- Content reference: [P0nk/Cosmic](https://github.com/P0nk/Cosmic) (Java, GMS v83) for
  party quests, events, bosses and quest/reactor/NPC flows the JMS reference lacks —
  never for bytes or opcodes (v83 differs from v186)
- Architecture template: [MS2Community/Maple2](https://github.com/MS2Community/Maple2) (C#)

Using the existing Java implementation JMSv186 as a "reference oracle", Cronus
reimplements the protocol, crypto, and game logic in C# — to fully understand and own
them as our own asset.

## Direction (2026-09-08)

The in-group milestone is reached: external players connected over the public IP. The goal
is now a **full rebuild of the JMS v186 game** that takes the best of three projects: Cosmic's
content completeness and easy setup-to-operations, JMSv186's byte-exact protocol fidelity,
and Maple2's modern .NET architecture. Two rules follow: nothing may crash the client, and
anything not yet implemented shows a `[DEV]` message instead of failing. The plan is
[docs/TASK.md](docs/TASK.md). Docker support was dropped the same day.

## Current status

A playable in-group server (all through the real encrypted protocol):

- **Login → world/character select → game entry** — the full entry path with the real
  JMS v186 client (handshake, AES-OFB, character creation, `LP_SetField`)
- **Field & combat** — multiplayer enter/leave, movement, chat/emotes/whispers, map
  transfer, mob spawns/AI delegation/respawn, melee/magic/ranged attacks with server-side
  damage bounding, mob skills (heal/summons), death & revive, HP/MP regen ticks
- **Items & economy** — inventory (all tabs), mob drops from real drop tables, NPC shops
  (meso + token currency, recharge), equip/unequip, **scrolling** (success/curse, clean
  slates, chaos, white scrolls), storage, gather/sort, portable chairs
- **Progression** — exp/level-ups with party sharing, SP → skills, buff skills with
  server-side expiry, quests (accept/complete with kill counters, rewards, lottery,
  quest scripts on Jint), skill macros, key bindings — all persisted
- **Social** — parties (survive relogs and channel switches), buddy list (offline adds,
  expandable capacity), guilds (create/invite/ranks/emblem/notice + guild chat), messenger,
  megaphones, trade, **Omok & match-card game rooms**, **personal shops**, **hired
  merchants** that keep selling while the owner is offline (and survive restarts), whisper
  and `/find` across channels
- **Multi-channel** — `CRONUS_CHANNELS` real channels with in-game channel change
- **Cash shop** — a real cash-shop server: browse, buy with an NX allowance
  (`CRONUS_NX`), an account locker, and moving items in and out of the inventory
- **Classes** — Adventurers, **Knights of Cygnus, and Aran**, with job advancement NPCs
  for every branch (1st–4th job)
- **Summons & doors** — puppets, hawks, dragons, beholder, pirate birds; **Mystic Door**
  with the party-window display
- **Towns** — 160+ scripted NPCs: world travel, storage, **beauty salons** (hair/face/
  skin with the style-picker UI), refiners, guild HQ, saunas, the Free Market, the janken
  master, and a name-aware greeting for everything unscripted
- **Scripts & data** — NPC / portal / quest scripts (JavaScript, Jint), wz_xml game data,
  the reference's SQL drop/shop tables, env-driven rates (`CRONUS_RATE_*`)

416 tests, all green. See the roadmap and design notes in
[AGENTS.md](AGENTS.md) / [CLAUDE.md](CLAUDE.md), and the real-client runbook in
[docs/GETTING_STARTED.md](docs/GETTING_STARTED.md).

> These tests prove internal consistency, not yet fidelity to the real Nexon client.
> [docs/VALIDATION.md](docs/VALIDATION.md) is the workflow to validate against a real JMS v186
> client with EmuClient + RirePE.

## Projects

| Project | Role |
|---|---|
| `Cronus.Common` | Region, ServerConfig, code page (MS932) |
| `Cronus.Domain` | Account / Character entities + repository ports |
| `Cronus.Network` | Crypto, framing, packet reader/writer, opcodes, session, listener |
| `Cronus.Database` | EF Core + Pomelo/MySQL persistence |
| `Cronus.Data` | wz_xml parser + map/portal data |
| `Cronus.Scripting` | Jint NPC conversation engine |
| `Cronus.Server.Core` | shared by the three processes: the gRPC contract (`proto/world.proto`), the World's state/service/client, start-up (.env, logs, opcodes, MySQL) |
| `Cronus.Server.World` | **process 1** — the hub: channel registry, presence, hand-offs, world-wide broadcasts, airship timetable (Maple2's World) |
| `Cronus.Server.Login` | **process 2** — authentication, world/channel list, character select |
| `Cronus.Server.Channel` | **process 3** — the game: N channels, cash shop, field ticks (Maple2's Game) |
| `Cronus.Debug.Bot` | content debugger: launches real client windows + headless verification bots |

## Build & run

```powershell
dotnet build Cronus.slnx -c Debug
dotnet test  Cronus.slnx -c Debug

# Start all three processes (World, Login, Channel) in one go:
run-server.bat                                       # Windows; stop-server.bat stops them
# or by hand, in this order, each from the repo root:
dotnet run --project src/Cronus.Server.World         # gRPC hub on 127.0.0.1:8585
dotnet run --project src/Cronus.Server.Login         # login 8484
dotnet run --project src/Cronus.Server.Channel       # channels 7575.. + cash shop
```

Requires .NET SDK 10.x. All optional integrations degrade gracefully when unset:

| Env var | Effect |
|---|---|
| `CRONUS_DB` | unset = MySQL (`Cronus186` from `CRONUS_DB_HOST/PORT/NAME/USER/PASSWORD`, created on first start); a connection string, `sqlite` or `memory` override it. Schema auto-creates and auto-migrates on upgrades |
| `CRONUS_WZ` | wz_xml data root; enables maps/NPCs/mobs/items/skills/quests |
| `CRONUS_SCRIPTS` | script root (`{root}/npc/{id}.js`, `portal/`, `quest/`); enables scripted content |
| `CRONUS_DROPS` | `drop_data.sql` dump; enables mob item/meso drop tables |
| `CRONUS_SHOPS` | `shops`+`shopitems` SQL dump; enables NPC shops (buy/sell) |
| `CRONUS_HOST` | The IP/hostname advertised to clients (LAN/public play); default localhost |
| `CRONUS_STARTMAP` | Map new characters start in (default 100000000, Henesys) |
| `CRONUS_RATE_EXP` / `_DROP` / `_MESO` | Server rate multipliers (default 1.0) |
| `CRONUS_AUTO_REGISTER` | `0`/`false` = only existing accounts may log in (default: unknown accounts auto-create on first login) |
| `CRONUS_CHANNELS` | Game channels to run, 1–8 (default 2) on consecutive ports from the channel port; in-game channel change works |
| `CRONUS_DEBUG` | `1` = log every packet (hex dumps) for protocol debugging; default off — heavy for real play |
| `CRONUS_NX` | Cash-shop NX allowance: every account is topped up to this floor on entering the shop (default 300000; `0` disables the cash shop) |

To connect a real client, point a JMS v186 client at the host via EmuClient's localhost
redirect. Accounts auto-register on first login. See
**[docs/GETTING_STARTED.md](docs/GETTING_STARTED.md)** for the full step-by-step runbook, and
**[docs/COMMANDS.md](docs/COMMANDS.md)** (日本語: [docs/COMMANDS.ja.md](docs/COMMANDS.ja.md)) for
the in-game `/` command reference.

### Try the bundled sample content

A minimal map (100000000) with one talkable NPC and a matching script ship in the repo:

```powershell
$env:CRONUS_WZ = "data/sample-wz"
$env:CRONUS_SCRIPTS = "scripts"
run-server.bat        # World, Login, Channel
```

New characters start in that map; the NPC (9010000) runs
[scripts/npc/9010000.js](scripts/npc/9010000.js) when clicked.

## License

[AGPL-3.0](LICENSE). Cronus is a derivative of upstream JMSv186 (GPLv3) and OdinMS-derived
code (AGPLv3), so it adopts the strongest applicable terms. Upstream credits are preserved
in [NOTICE.md](NOTICE.md).
