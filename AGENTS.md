# AGENTS.md — Cronus Design, Roadmap, and Task Board

This file is the shared **project contract** for agents (and humans). The operational
guide (build steps, protocol spec, coding conventions) lives in [CLAUDE.md](CLAUDE.md);
this file holds the **rationale for design decisions, the roadmap, and the tasks (work and
improvements)**. The two files are complementary — if you find a contradiction, update both.

---

## 0. One-line summary

> Reimplement the core of a JMS v186 private server in C#/.NET, using Riremito/JMSv186
> (Java) as a side-by-side oracle. Get login working first, then widen in vertical slices.

### Final goal (revised 2026-09-08)

> **Rebuild the MapleStory of that era (JMS v186) in full**, as the server *and* client
> package that takes the best of three projects: **Cosmic** (content completeness, and a
> setup-to-operations experience that stays simple), **JMSv186** (byte-exact fidelity to the
> JMS v186 client; the protocol oracle), and **Maple2** (a modern, maintainable .NET
> architecture). The measure is **fidelity first**, the highest reproduction of the original
> game, with setup, backup and maintenance that anyone can do from the docs.

Why the change: the 2026-08-21 goal below was reached on 2026-09-08 (external players
connected over the public IP), but many contents crashed the client. "Minimal playability"
was the wrong bar; the bar is now "everything the v186 client contains".

Ground rules that follow (details in [docs/TASK.md](docs/TASK.md)):
1. **No crashes.** Anything not implemented degrades to a visible `[DEV]` message, never to
   silence and never to a client crash.
2. **`[DEV]` marking.** Every player-visible text for unimplemented, partial or invented
   content carries `[DEV]` and says what is missing (`cm.sendDev(...)` in scripts,
   `NpcConversation.DevPrefix` in C#).
3. **Two references, one oracle.** JMSv186 remains the only source for bytes, opcodes,
   timing and enums. Cosmic (GMS v83, `Reference/Cosmic`) is the reference for content flow
   (party quests, events, area bosses, quest/reactor/NPC logic), always re-checked against
   v186 data. Maple2 is the architecture template.
4. Docker support was dropped on 2026-09-08; the bat scripts plus `.env` are the only setup path.

### Final goal (set 2026-08-21, reached 2026-09-08, superseded by the above)

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

## 4. Roadmap and backlog

The roadmap, the task board and the backlog live in **[docs/TASK.md](docs/TASK.md)** (the
2026-09-08 plan: crash elimination, the `[DEV]` rule, then every content area of the v186
client to 100%). The 2026-08 milestone log (M1–M18: login, field, combat, items, social,
cash shop, deploy rehearsal) was removed from this file on 2026-09-08; it remains in git
history before the `INITIAL COMMIT 2` commit.

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
reference; entry sequence ported). Details: `docs/CLIENT_PATCHES.md` (NameSpace.dll fix),
root `CLIENT_RENDERING_FIX.md`.

## 8. Agent Operating Rules

- **Commit frequently and push** (per meaningful unit). Branch flow since 2026-09-09: one branch
  per **test item** (a verifiable piece of work), `feat/<item>` or `fix/<item>` cut from `develop`;
  every commit of that item goes there and is pushed there. The item is done when its tests are
  green (`dotnet test`, the bot suite, and a real-client check when the change can crash the
  client); then `git merge --no-ff` into `develop`, push, delete the branch (local and origin).
  When a systematic body of work is finished and `develop` runs stably on the real client, merge
  `develop` into `main` (`--no-ff`) and add an annotated tag `stable-N` (next after `stable-1`,
  message = what was verified), `git push origin main --tags`. Never commit to `main` directly.
  Docs-only / generated-report-only updates may go straight to `develop`.
- Any change touching byte boundaries must be committed **together with its test**.
- Do not fill unknown protocol behavior by guessing; ground it in the relevant JMSv186
  code. Mark ungrounded spots as TODO and add them to the Backlog.
- Reference order for content: JMSv186 (bytes, timing, enums), then Cosmic (flow of party
  quests, events, bosses, quests, reactors, NPCs; re-check every id and number against v186
  data), then invention, which must be marked. Never take bytes, opcodes or crypto from Cosmic.
- Every player-visible text about unimplemented, simplified or invented content carries
  `[DEV]` (`cm.sendDev`, `NpcConversation.DevPrefix`). Unimplemented entry points must end
  in such a message or a harmless refusal; a client crash or a silent lock is always a bug.
- Avoid destructive / irreversible operations (force push, history rewrite, deletion).
- Update this file and CLAUDE.md whenever a design decision changes (don't let docs rot).
- Write documentation in English.
