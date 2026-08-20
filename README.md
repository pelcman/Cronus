# Cronus

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
- **Field** — multiplayer enter/leave, movement relay, chat, map transfer (portals)
- **NPC dialogue** — JavaScript scripts (Jint) drive `LP_ScriptMessage` conversations

61 tests, all green. See the roadmap and design notes in
[AGENTS.md](AGENTS.md) / [CLAUDE.md](CLAUDE.md).

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

To connect a real client, point a JMS v186 client at the host via EmuClient's localhost
redirect. Accounts auto-register on first login.

## License

[AGPL-3.0](LICENSE). Cronus is a derivative of upstream JMSv186 (GPLv3) and OdinMS-derived
code (AGPLv3), so it adopts the strongest applicable terms. Upstream credits are preserved
in [NOTICE.md](NOTICE.md).
