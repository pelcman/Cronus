# AGENTS.md — Cronus Design, Roadmap, and Task Board

This file is the shared **project contract** for agents (and humans). The operational
guide (build steps, protocol spec, coding conventions) lives in [CLAUDE.md](CLAUDE.md);
this file holds the **rationale for design decisions, the roadmap, and the tasks (work and
improvements)**. The two files are complementary — if you find a contradiction, update both.

---

## 0. One-line summary

> Reimplement the core of a JMS v186 private server in C#/.NET, using Riremito/JMSv186
> (Java) as a side-by-side oracle. Get login working first, then widen in vertical slices.

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
- [ ] **M2: Hello handshake wire-up** — accept TCP → send Hello → client syncs encryption.
      Pipelines codec + session. Confirm handshake byte-match with RirePE. **← current**
- [ ] **M3: Login authentication** — handle `CP_CheckPassword`, reply
      `LP_CheckPasswordResult`. Login screen → ID/PW auth passes. **(near-term key goal)**
- [ ] **M4: World / channel select** — world list, channel select, character list.
- [ ] **M5: Character select → channel migrate** — MigrateCommand to the channel server.
- [ ] **M6: Map entry** — spawn the character into a field, sync movement.
- [ ] **M7: Basic gameplay** — inventory, NPC dialogue (Jint scripts), simple combat.

Reaching a "playable core" (combat, inventory, NPC) is a multi-week effort; full v186
parity is on the order of half a year.

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

### Next (M2–M3)
- [ ] `MapleCodec` (Pipelines-based encoder/decoder)
- [ ] `MapleSession` (holds IV pair, sends Hello, dispatches receives)
- [ ] TCP listener (Login/Channel)
- [ ] `Cronus.Server.Login`: Hello → CheckPassword handler
- [ ] Account auth (interim: in-memory or simple SQLite/MySQL)

### Improvements / tech debt (ongoing)
- [ ] **Add golden vectors**: run the Java build, capture handshake→login real bytes with
      RirePE, pin them in tests (currently round-trip only).
- [ ] Test AES-OFB block handling (0x5B0/0x5B4) with large payloads.
- [ ] Move to `Span<byte>`/`Memory<byte>`-centric APIs to cut allocations (naive first).
- [ ] Warn on undefined opcodes (@FFFF) at startup.
- [ ] Externalize ports / DB connection / data paths via appsettings.json.
- [ ] Docker Compose (bundled MySQL) — reference Maple2's compose.yml.
- [ ] CI (GitHub Actions: build + test).

---

## 6. Repository Layout (current → target)

```
Cronus/
├─ CLAUDE.md / AGENTS.md / README.md / LICENSE / NOTICE.md
├─ .gitignore / .editorconfig / Directory.Build.props
├─ Cronus.sln
├─ src/
│  ├─ Cronus.Common/
│  ├─ Cronus.Network/            (Crypto/, Packets/)
│  ├─ Cronus.Database/           (later)
│  ├─ Cronus.Data/               (later)
│  ├─ Cronus.Scripting/          (later)
│  ├─ Cronus.Server.Login/       (later)
│  ├─ Cronus.Server.Channel/     (later)
│  └─ Cronus.Server.Host/        (later)
├─ tests/
│  └─ Cronus.Network.Tests/
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

## 8. Agent Operating Rules

- **Commit frequently and push** (per meaningful unit). Feel free to use a working branch.
- Any change touching byte boundaries must be committed **together with its test**.
- Do not fill unknown protocol behavior by guessing; ground it in the relevant JMSv186
  code. Mark ungrounded spots as TODO and add them to the Backlog.
- Avoid destructive / irreversible operations (force push, history rewrite, deletion).
- Update this file and CLAUDE.md whenever a design decision changes (don't let docs rot).
- Write documentation in English.
