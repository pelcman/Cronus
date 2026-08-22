# CLAUDE.md — Cronus Development Guide

This file is the working guide for AI agents (Claude Code and others) and human
developers in the Cronus repository. It captures the **design philosophy, the concrete
design, and how to work in this repo**. For the detailed roadmap and task board, see
[AGENTS.md](AGENTS.md).

---

## 1. Project Overview

**Cronus** is an open-source private-server emulator for the Japanese version of
MapleStory (JMS / JapanMS). The network core and the heart of the game logic are
reimplemented in **C# / .NET**.

- **Target client**: JMS **v186** (the last stable line before Big Bang).
- **Reference implementation**: [Riremito/JMSv186](https://github.com/Riremito/JMSv186)
  (Java). It is currently the only actively developed JMS server implementation, and it
  serves as Cronus' **reference oracle**, run side by side.
- **Companion tools (reused as-is)**:
  - [Riremito/EmuClient](https://github.com/Riremito/EmuClient) — client-side DLL
    injection (localhost redirect, CRC bypass). Verified on JMS v164/165/186/188/194.
  - [Riremito/RirePE](https://github.com/Riremito/RirePE) — packet editor / analyzer.
    The core tool for **differential packet verification** between the C# and Java builds.

### Why rewrite Java into C#

The goal is not "avoiding Java" but **fully understanding and owning the protocol and
game logic as our own asset**. Java→C# is a best-in-class port target: the language
constructs map almost 1:1, so even at ~86k lines the mechanical-translation ratio is
high. Because a live Java implementation can act as an oracle, we can mechanically detect
the thing that hurts most in a rewrite — subtle behavioral drift.

---

## 2. Design Principles

1. **Always-Green.** Build in vertical slices. Get one thin path all the way through
   (login screen → character select → map entry) before widening. Don't accumulate large
   unconnected code.

2. **Differential Correctness.** Byte-level correctness is judged by "does it emit the
   same bytes as the Java build?", not "does my code look right?". Capture Java packets
   with RirePE and pin them as golden vectors in tests.

3. **YAGNI — drop multi-version support up front.** JMSv186's complexity mostly comes from
   supporting KMS/CMS/TWMS/… across v131–308. Cronus targets **JMS v186 only**. We still
   keep opcodes and region differences as external *data* so future expansion stays open.

4. **Separate protocol from game logic.** Keep the network / crypto / packet-serialization
   layer (which must be hand-written) loosely coupled from the game rules (ported from
   Java).

5. **Reuse data as an external asset.** wz_xml (game data), the MySQL schema, opcode
   `.properties`, and JavaScript scripts do not depend on the server's implementation
   language — reuse them unchanged. Scripts run on **Jint** (a C# JS engine), matching the
   Nashorn approach used by the Java side.

6. **Follow modern .NET idioms.** Don't port MINA; rewrite the async I/O on
   `System.IO.Pipelines`. Use the DI / structured-logging / EF Core stack that
   Maple2 (MS2Community) established as a template.

### Networking design reference (conceptual)

We do **not** use Valve's SDK, but the
[Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
model is a useful lens for reasoning about Cronus' networking and game loop. Map the concepts
onto our slower, event-driven 2D MMORPG (not a fast FPS):

- **Server authority (most applicable).** The server is the single source of truth: it owns
  world state, validates combat, and grants exp/drops. Cronus should trend *more* authoritative
  over time. Combat damage is still client-reported (the MapleStory norm) but no longer trusted
  verbatim: `DamageValidator` bounds every line to the pre-Big-Bang cap of 99,999 (M18). Remaining
  soft spots to tighten: per-skill/weapon damage ceilings from wz, attack-rate limiting, range
  checks, and client-authoritative movement.
- **Tick / fixed simulation step.** Server-owned periodic work — mob AI/respawn, buff and drop
  expiry, spawn timers — belongs on a server tick, decoupled from client packets.
- **Input commands.** Client → server packets are *commands* the server processes; never treat
  them as trusted state.
- **"Send only what changed" (delta idea).** MapleStory is event-based (spawn/leave/move/stat
  packets) rather than full-world snapshots, but the principle — push minimal per-change updates
  to only the clients that need them (same field) — is exactly how `Field`/broadcast works.
- **Client prediction / interpolation (adapted).** MapleStory pushes prediction to the extreme:
  the local player is client-authoritative for movement and *sends its own CMovePath*, which the
  server relays verbatim to others (our `CP_UserMove` → `LP_UserMove`). Remote entities are drawn
  from those relayed paths. Full snapshot interpolation / lag compensation (FPS-grade) are **not**
  applicable here.

Takeaway: keep the server authoritative and event-driven; put timed world logic on a tick; treat
all client input as untrusted commands; broadcast minimal deltas per field.

---

## 3. Architecture (Project Layout)

Modeled on the Maple2 (MS2Community/Maple2) solution split, adapted for MS1 / JMS.
**Start single-process** (Login + Channel in one process with an in-process World
registry), structured so it can later be split behind gRPC. Physical World-server split
and gRPC are out of initial scope.

```
Cronus.slnx
├─ src/
│  ├─ Cronus.Common          … Region / ServerConfig / constants / code page
│  ├─ Cronus.Domain          … Account, IAccountRepository (ports); in-memory adapter
│  ├─ Cronus.Network         … ★ hand-written core: crypto, framing, PacketReader/Writer,
│  │                            opcodes, MapleSession, MapleListener
│  ├─ Cronus.Database        … EF Core + Pomelo (MySQL): CronusDbContext, DbAccountRepository
│  ├─ Cronus.Data            … wz_xml loader (≒ Maple2.File.Ingest / odin.provider.WzXML) [later]
│  ├─ Cronus.Scripting       … Jint. Reuse existing JS scripts (NPC/quest) [later]
│  ├─ Cronus.Server.Login    … login server (LoginHandler/Service/Packets, World)
│  ├─ Cronus.Server.Channel  … channel / game logic [later]
│  └─ Cronus.Server.Host     … entry point (config, startup); MySQL via CRONUS_DB env var
└─ tests/
   ├─ Cronus.Network.Tests       … crypto round-trip, header, opcode, session, listener
   ├─ Cronus.Server.Login.Tests  … login + world-select flow (end-to-end, encrypted)
   └─ Cronus.Database.Tests      … repository + login-over-DB (EF Core InMemory provider)
```

Dependency direction: `Server.* → {Network, Database, Domain, Common}`, `Database → Domain`,
`Login → Domain`. `Network` depends only on `Common` (knows nothing about game logic).
`Domain` holds the ports (repository interfaces) so infrastructure depends inward.

### JMSv186 (Java) → Cronus (C#) mapping

| JMSv186 (Java) | Cronus (C#) | Notes |
|---|---|---|
| `tacos.network.MapleAESOFB` | `Cronus.Network.Crypto.AesOfbCipher` | AES-OFB core, incl. IV advance |
| `tacos.network.MapleCustomEncryption` | `Cronus.Network.Crypto.ShandaCipher` | Unused by JMS, ported anyway |
| `tacos.network.PacketDecoder/Encoder` | `Cronus.Network.MapleCodec` | Framing, rewritten on Pipelines |
| `tacos.packet.ServerPacket` | `Cronus.Network.Packets.PacketWriter` | COutPacket equivalent (LE writes) |
| `tacos.packet.ClientPacket` | `Cronus.Network.Packets.PacketReader` | CInPacket equivalent (LE reads) |
| `tacos.packet.ClientPacketHeader` (enum) | `Cronus.Network.Packets.ClientOpcode` | opcode name identifiers |
| `tacos.packet.ServerPacketHeader` (enum) | `Cronus.Network.Packets.ServerOpcode` | 〃 |
| `tacos.property.Property_Packet` | `Cronus.Network.Packets.OpcodeTable` | loads values from .properties |
| `odin.client.MapleClient` | `Cronus.Server.*` session types | session state |
| `tacos.server.TacosLogin` | `Cronus.Server.Login` | |
| `odin.provider.WzXML` | `Cronus.Data` | wz_xml loader |
| Nashorn scripts | `Cronus.Scripting` (Jint) | JS reused nearly verbatim |

---

## 4. Protocol Notes (facts established for JMS v186)

The most important invariants for implementation and review. Sources: JMSv186's
`tacos/network` and `tacos/config`.

### Connection handshake
1. Server accepts the TCP connection.
2. Generate two IVs: `serverRecv = {70,114,122,rand}`, `serverSend = {82,48,120,rand}`.
3. Send a **plaintext Hello packet** (before encryption starts):
   `[dataSize:2(LE)] [version:2(LE)] [subVersionStr] [recvIv:4] [sendIv:4] [region:1]`
   - `dataSize` is the Hello body length (excluding the leading 2 bytes).
   - `subVersionStr` is a length-prefixed string ("0" for JMS).
   - `region` is JMS = **3**.
4. Both sides then begin encrypting.

### Frame format (after Hello)
`[encrypted header:4] [encrypted body]`
- Header generation: `iiv = (iv[3] | (iv[2]<<8)) ^ mapleVersion`,
  `mlen = byteswap16(length) ^ iiv`,
  bytes = `[(iiv>>8), iiv, (mlen>>8), mlen]`.
- Length recovery: `len = byteswap16((hdr>>16) ^ (hdr & 0xFFFF))`.
- `mapleVersion` = send side `byteswap16(0xFFFF - VERSION)`, receive side `byteswap16(VERSION)`.

### Crypto (resolved values for JMS v186)
- **AES key**: JMSv186 default 32-byte skey (`13 00 00 00 08 00 00 00 06 …`).
- **CustomEncryption (shanda)**: **false** for JMS (not applied).
- **OldIV**: true when `VERSION <= 141`. v186 is **false** → `multiplyBytes(iv,4,4)`
  (the 4-byte IV repeated to 16 bytes) seeds the keystream.
- **PacketHeaderSize**: 2.
- **AES-OFB body**: split into 0x5B0 (first) / 0x5B4 (subsequent) byte blocks; within each
  block, every 16 bytes update `keystream = AES-ECB-encrypt(previous keystream)` and XOR.
  ECB, no padding, 16-byte units.
- **IV advance**: per packet, `iv = getNewIv(iv)` (apply funnyShit four times over the
  magic `{0xf2,0x53,0x50,0xc6}`). Send and receive advance independently.

### Packet serialization
- Everything is **little-endian**. `Encode1/2/4/8`, `EncodeStr` (`[len:2][bytes]`, code
  page **MS932 / Shift-JIS**), `EncodeBuffer`.
- On .NET Core, Shift-JIS requires registering
  `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` at startup.

### Opcode tables
- `ClientOpcode` / `ServerOpcode` are **name identifiers shared across all versions**.
- Concrete numbers load from `data/opcodes/JMS_v186_ClientPacket.properties` /
  `..._ServerPacket.properties`.
- Format: `CP_CheckPassword = @0001`, or `NAME = BASE + offset` (BASE may reference another
  opcode name in the same file). Undefined = `@FFFF` (invalid).
- Reference values (JMS v186): `CP_CheckPassword=@0001`, `LP_CheckPasswordResult=@0000`.

---

## 5. Build, Test, Run

> **Prerequisite**: .NET SDK 10.x installed (`dotnet --list-sdks`). Target framework `net10.0`.

```powershell
# Build the whole solution
dotnet build Cronus.sln -c Debug

# Run tests (crypto round-trip, etc.)
dotnet test tests/Cronus.Network.Tests

# Run the host (login server; default port 8484, or pass a port)
dotnet run --project src/Cronus.Server.Host          # persists to cronus.db (SQLite) by default
dotnet run --project src/Cronus.Server.Host 9595     # custom port

# Storage backends: unset = SQLite file (CRONUS_DB_FILE overrides the path);
# a MySQL connection string switches to MySQL; "memory" = in-process only.
$env:CRONUS_DB = "server=localhost;database=cronus;user=root;password=..."
dotnet run --project src/Cronus.Server.Host
```

### Verification strategy (important)
- Unit tests are **round-trip** based (send-encrypt → mirrored recv-decrypt recovers the
  plaintext, and the IVs advance in lockstep).
- Additionally, pin **golden vectors captured from the Java build via RirePE** (TODO: add
  once the Java build runs; until then round-trip provides the guarantee).
- When porting game logic, diff the C# and Java outbound packets for the same action with
  RirePE, using zero-diff as the regression bar.

---

## 6. Coding Conventions

- C# 12+ / .NET 10. `nullable enable`, `ImplicitUsings enable`.
- .NET-standard naming (PascalCase types/methods, camelCase locals, `_camelCase` private
  fields).
- Never change Java-derived constants / magic numbers (AES key, funnyBytes, etc.). Instead
  of provenance comments, state the corresponding Java class once at the top of the file.
- Do not guess crypto / framing. Always ground it in the specific Java source.
- Any change that shifts a packet byte boundary must be committed together with its test.

---

## 7. License

Upstream JMSv186 mixes **GPLv3** (most of Riremito's code) and **AGPLv3** (OdinMS-derived
code such as `MapleAESOFB`). Cronus, which ports this code, is a derivative work and is
distributed under **AGPL-3.0** to match the strongest applicable terms. Upstream copyright
notices and credits are preserved. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

## 8. Legal / Ethical Notice

MapleStory is Nexon's intellectual property. Operating a public private server carries high
legal risk in Japan. **This project is strictly for local, research, and educational use.**
No commercial or public operation.

---

## 9. Current Status and Next Step

Detailed progress and the task board live in [AGENTS.md](AGENTS.md) ("Roadmap", "Backlog").

- **Done**: the full path works with the real JMS v186 client — login → character select → game
  entry — plus a single-process channel serving movement, chat, combat (melee/magic/ranged with
  server-side damage bounding, mob skills incl. heal/summons), mob respawn/control, HP/MP regen,
  death & revive, the full **item economy** (inventory on all tabs, mob drops from `drop_data.sql`,
  equip drops carrying instances, NPC shops with token currency + recharge, equip scrolling,
  storage, gather/sort), **progression** (exp/level-ups with party share, SP → skills, buff skills
  with server-side expiry, quests incl. kill counters + lottery rewards + Jint quest scripts, skill
  macros, key bindings), and a complete **social layer**: whisper/`/find`, emotes, chairs,
  messenger, parties, buddy list (offline adds), guilds (+guild/party/friend chat), megaphones,
  trade, Omok/match-card rooms, personal shops, and hired merchants that persist across restarts.
  In-game commands use the `/` prefix (docs/COMMANDS.md, EN/JA). Persistence defaults to a
  SQLite file (zero setup; EF Core), with MySQL (Pomelo) for production via `CRONUS_DB`; deploy
  is env-driven (`CRONUS_HOST`/`CRONUS_DB`/`CRONUS_WZ`/`CRONUS_SCRIPTS`/`CRONUS_DROPS`/
  `CRONUS_SHOPS`/`CRONUS_RATE_*`), fed by a repo-root `.env` file (Maple2-style; see
  `.env.example` — real environment variables override it).
- **Deferred**: guild BBS (its LP opcode is unresolved even in the reference), alliances,
  mastery books (skills level to wz max directly — an intentional simplification), mob stat
  buffs/player diseases (dead code in the reference), mini-game invites via the game UI.
- **Verification**: unit round-trips + golden vectors + end-to-end tests through encrypted sessions
  (300+ tests). Most of the newer systems are byte-verified against the Java oracle but not yet
  re-tested with the live client — that client pass is the next milestone.
