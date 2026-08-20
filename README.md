# Cronus

An open-source private-server emulator for the Japanese version of MapleStory
(JMS / JapanMS **v186**). The network core and the heart of the game logic are
implemented in **C# / .NET 10**.

> ⚠️ **Note**: This project is strictly for local, research, and educational use.
> MapleStory is Nexon's intellectual property; we do not operate public or commercial
> private servers.

## What this is

- Target client: **JMS v186** (the last stable line before Big Bang)
- Reference implementation (side-by-side oracle):
  [Riremito/JMSv186](https://github.com/Riremito/JMSv186) (Java)
- Client-side tools: [EmuClient](https://github.com/Riremito/EmuClient) /
  [RirePE](https://github.com/Riremito/RirePE) (reused as-is)

Using the existing Java implementation JMSv186 as a "reference oracle" run side by side,
we reimplement the core of the protocol, crypto, and game logic in C#. The goal is to
fully understand and own the protocol and logic as our own asset.

## Development documents

- **[CLAUDE.md](CLAUDE.md)** — operational guide (build/test, protocol spec, conventions)
- **[AGENTS.md](AGENTS.md)** — design philosophy, roadmap, task board

## Build

```powershell
dotnet build Cronus.sln -c Debug
dotnet test tests/Cronus.Network.Tests
```

Requires: .NET SDK 10.x.

## Status

Early development. Current milestone: **M1 — Network core** (crypto, packet
serialization, opcode loader, and unit tests). See the roadmap in
[AGENTS.md](AGENTS.md).

## License

[AGPL-3.0](LICENSE). Because Cronus is a derivative of upstream JMSv186 (GPLv3) and
OdinMS-derived code (AGPLv3), it adopts the strongest applicable terms. Upstream credits
are preserved — see [NOTICE.md](NOTICE.md).
