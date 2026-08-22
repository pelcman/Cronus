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

Using the existing Java implementation JMSv186 as a "reference oracle", Cronus
reimplements the protocol, crypto, and game logic in C# — to fully understand and own
them as our own asset.

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
- **Social** — parties (invite/accept/decline, HP bars, exp share), buddy list (offline
  adds included), guilds (create/invite/ranks/emblem/notice + guild chat), messenger,
  megaphones, trade, **Omok & match-card game rooms**, **personal shops**, **hired
  merchants** that keep selling while the owner is offline (and survive restarts)
- **Scripts & data** — NPC / portal / quest scripts (JavaScript, Jint), wz_xml game data,
  the reference's SQL drop/shop tables, env-driven rates (`CRONUS_RATE_*`)

406 tests, all green. See the roadmap and design notes in
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
| `Cronus.Server.Login` | login server |
| `Cronus.Server.Channel` | channel / in-game server |
| `Cronus.Server.Host` | runnable host (login + channel) |

## Build & run

```powershell
dotnet build Cronus.slnx -c Debug
dotnet test  Cronus.slnx -c Debug

# Run the host (login on 8484, channel on 7575 by default)
dotnet run --project src/Cronus.Server.Host
dotnet run --project src/Cronus.Server.Host 8484 7575
```

Requires .NET SDK 10.x. All optional integrations degrade gracefully when unset:

| Env var | Effect |
|---|---|
| `CRONUS_DB` | MySQL connection string; else in-memory accounts/characters. Schema auto-creates and auto-migrates on upgrades |
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
dotnet run --project src/Cronus.Server.Host
```

New characters start in that map; the NPC (9010000) runs
[scripts/npc/9010000.js](scripts/npc/9010000.js) when clicked.

### Docker (with MySQL persistence)

```bash
docker compose up --build
```

Starts MySQL + the host (login 8484, channel 7575) with persistent accounts/characters.
Mount a wz_xml tree into the `cronus` service and set `CRONUS_WZ` to enable maps/NPCs.

## License

[AGPL-3.0](LICENSE). Cronus is a derivative of upstream JMSv186 (GPLv3) and OdinMS-derived
code (AGPLv3), so it adopts the strongest applicable terms. Upstream credits are preserved
in [NOTICE.md](NOTICE.md).
