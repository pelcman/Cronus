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

A playable slice is up and running (all through the real encrypted protocol):

- **Login** — handshake + AES-OFB crypto, `CP_CheckPassword` → `LP_CheckPasswordResult`
- **World / character select** — world list, world/channel select, character list,
  name check, character creation
- **Game entry** — `CP_MigrateIn` → `LP_SetField` (full CharacterData)
- **Field** — multiplayer enter/leave, movement relay, chat, map transfer (portals),
  NPC/mob spawns, mob controller movement
- **NPC dialogue** — JavaScript scripts (Jint) drive `LP_ScriptMessage` conversations, with a
  player API (meso/exp/hp, quests, skills)
- **Items** — equipment serialization + persistence; starter equips on creation
- **Combat & progression** — melee attack (mirrored to others) → mob HP → death → exp
  (level-up) → meso drops → pickup; SP → skills; script-driven quests
- **Keep-alive** — the server pings idle clients (`LP_AliveReq`) to hold the connection

93 tests, all green. See the roadmap and design notes in
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
| `CRONUS_DB` | MySQL connection string; else in-memory accounts/characters |
| `CRONUS_WZ` | wz_xml data root; enables portal-by-name transfers |
| `CRONUS_SCRIPTS` | script root (`{root}/npc/{id}.js`); enables NPC dialogs |
| `CRONUS_DROPS` | `drop_data.sql` dump; enables mob item/meso drop tables |

To connect a real client, point a JMS v186 client at the host via EmuClient's localhost
redirect. Accounts auto-register on first login. See
**[docs/GETTING_STARTED.md](docs/GETTING_STARTED.md)** for the full step-by-step runbook.

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
