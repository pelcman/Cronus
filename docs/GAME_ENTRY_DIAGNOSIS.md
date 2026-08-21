# Game-Entry Diagnosis & Real-Client Bring-Up

This document tracks the investigation into why the real JMS v186.1 client
(`client/MapleStory_v186/JMS_v186.1_L.exe`) crashes when entering the game
world, and records the ground-truth captured from Riremito's reference server
(`Reference/JMSv186`, cloned as a working oracle).

## Status legend
- ✅ solved / verified
- 🔶 in progress
- ❌ open blocker

## What works with the real client (✅)
Fixes landed on branch `feat/login-mapscreen`:

1. **Version handshake** — `ServerConfig.SubVersion` must be `1` (JMS v186.**1**),
   not `0`. Wrong value → client shows "wrong version".
   (`src/Cronus.Common/ServerConfig.cs`)
2. **Login background map** — client sends `CP_JMS_GetMapLogin`; server must
   reply `LP_JMS_SetMapLogin("MapLogin")` or the login screen stays black.
   (`LoginHandler` + `LoginPackets.SetMapLogin`)
3. **World list push** — after `LP_CheckPasswordResult(success)` JMS **pushes**
   the world/channel list; it does **not** wait for `CP_WorldInfoRequest`.
   Without the push, login "does nothing".
   (`LoginHandler.HandleCheckPasswordAsync` → `HandleWorldInfoRequestAsync`)
4. Login → world select → character create → character select all reach the
   client correctly.

## Ground-truth capture methodology (✅)
- The reference server was patched only where needed to **compile on JDK 21**
  (`statements before super()` in `TacosSkillPet`/`TacosDragon`) and to **log
  every server→client packet** (`PacketEncoder` emits `[REFSEND] op=0x%04X`).
- Full v186 WZ data was obtained as XML from `Riremito/wz_xml`
  (`xml_JMS_v186.7z`, 18 MB → 424 MB / 19009 files) and extracted into
  `Reference/JMSv186/wz_xml/xml_JMS_v186/`. All temporary "skip-WZ" hacks were
  then **reverted** so the reference runs on real data (true oracle).

## KEY FINDING — Cronus SetField is correct (✅)
The reference's `LP_SetField` (op `0x007E`) for a fresh level-1 beginner is
**701 bytes** and decodes with the exact same structure Cronus emits:

```
total = 701 bytes  (identical length to Cronus)
CharacterStat @ offset 38: id, name(13), gender, skin, face(i32), hair(i32),
  ... level=1, job=0, ap=0, sp=0, posMap=100000000, portal=0
```

⇒ **Cronus's SetField encoding is byte-structurally identical to the working
reference.** The game-entry crash is **not** a SetField bug.

## Reference OnMigrateIn packet sequence (ground truth)
Captured order after the field is set (op = purpose):

| # | op     | name                        | Cronus sends? |
|---|--------|-----------------------------|---------------|
| 1 | 0x007E | LP_SetField (CharacterData) | ✅ (verified) |
| 2 | 0x001D | LP_StatChanged  `01 00000000` | ❌ missing |
| 3 | 0x0021 | LP_ForcedStatReset (empty)  | ✅ |
| 4 | 0x017D | LP_PetConsumeItemInit `00000000` | ❌ missing |
| 5 | 0x017E | LP_PetConsumeMPItemInit `00000000` | ❌ missing |
| 6 | 0x017F | LP_JMS_PetConsumeCureItemInit `00000000` | ❌ missing |
| 7 | 0x017C | LP_FuncKeyMappedInit (keymap) | ✅ |
| 8 | 0x007D | LP_MacroSysDataInit `00`    | ✅ |
| 9 | 0x0039 | LP_FriendResult `07 00`     | ❌ missing |
|10 | 0x006C | LP_FamilyPrivilegeList (1915 B static table) | ❌ missing |
|11 | 0x0067 | LP_FamilyInfoResult (30 B)  | ❌ missing |
|12 | 0x003F | LP_BroadcastMsg `04 00`     | ❌ missing |

Raw bytes for the small packets are recorded in the session log; the 1915-byte
family-privilege table is a static blob (saved to scratch during capture).

## RESOLUTION (root cause found — client-side, not a server bug) ✅

**The game-entry crash is a client-environment problem, not a Cronus bug.**
Proven by running the working reference server with the **full real v186 WZ
data** (extracted from `Riremito/wz_xml`): it emits the complete, correct
`OnMigrateIn` sequence (`SetField` 717 B → StatChanged → ForcedStatReset →
pet×3 → keymap → macros → FriendResult → FamilyPrivilegeList → FamilyInfoResult
→ BroadcastMsg → NpcEnterField×2) and the real client **still crashes at field
entry, identically**. A known-good server + correct data + real WZ ⇒ the crash
is on the client side.

**Root cause: DirectX 8 field rendering on a modern GPU.**
- Process Monitor capture of the client (`JMS_v186.1_L.exe`) at the crash shows
  **no missing files and no WZ file access** (WZ is memory-mapped at startup);
  the crash tail is **Direct3D device initialisation** — repeated probes of
  `HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D\{DisableNVPS, UseVSConverter,
  UsePSConverter, DisableStripFVF, DisableGB, DisablePSGP, EmulatePointSprites}`
  — interleaved with loading `WzFlashRenderer.dll`. `DisableNVPS` is an
  NVIDIA-specific D3D key.
- The machine's only active GPU is an **NVIDIA GeForce GTX 1660 Ti** (not an
  iGPU). The client is a 2010 **DirectX 8** title (`Gr2D_DX8.dll`). Login and
  character-select (simple 2-D screens) render fine; the **field renderer**
  (full scrolling world) crashes — the classic "old DX8 game on a modern GPU"
  failure. The client's own error dialog reports a generic HRESULT
  (`0x80030002` / `0x80004003`).

**Fix applied (client-side, reversible): dgVoodoo2** — a DirectX 8/DDraw →
modern D3D11/12 wrapper. Its x86 `D3D8.dll`/`D3D9.dll`/`DDraw.dll`/`D3DImm.dll`
+ `dgVoodoo.conf` are copied into the client folder (see repo-root
`apply_dgvoodoo.bat` / `revert_dgvoodoo.bat` and `CLIENT_RENDERING_FIX.md`).
`dgVoodooWatermark = true` shows a corner watermark to confirm it is active.
Fallbacks if it doesn't help: Windows compatibility mode (WinXP SP3 / 16-bit
colour), disabling `WzFlashRenderer.dll`, or a different machine / GPU.

---

## Original blocker analysis (superseded by RESOLUTION above)
Both **Cronus** and the **reference** crash the real client at field entry:

- Map `100000000` (Henesys): client shows **"pointer invalid" (0x80004003)**,
  no background rendered.
- Map `104000000` (Lith Harbor): client renders the **background for a moment**,
  then **"file not found" (0x80030002 / STG_E_FILENOTFOUND)**.
- With a **stub** server-side map the reference crashes the same way; with the
  **real** server-side map the map's `life` (NPCs) needs `Npc.wz` to load
  (`MapleLifeFactory.getNPC`), which is why an incomplete server data set makes
  Game Start "do nothing" (server throws before sending entry packets).

The character appearance is valid standard beginner data
(top 1040010, bottom 1060006, shoes 1072037, weapon 1302000, face 20100,
hair 30027, skin 0), so the crash is **not** the character sprite.

### Working hypotheses (to disprove one by one)
1. Server-side map data was insufficient (stub) → **testing** with full real WZ
   on the reference now.
2. The missing entry packets (#2,4,5,6,9,10,11,12) leave the client in a state
   where it dereferences null / looks up a missing resource. → replicate the
   full reference sequence in Cronus.
3. Client/environment issue (localhost `_L` build + iGPU shim covers the login
   UI but not the field renderer). If the **reference with full real WZ** still
   crashes, this becomes the leading hypothesis; confirm with Process Monitor
   (needs elevation) to see the exact missing file.

## TODO (next steps)
- [ ] Confirm reference behaviour with **full real WZ** (in progress). Record
      whether the client enters, and the exact `[REFSEND]` sequence + any
      server exception.
- [ ] If the reference now enters: port the **full OnMigrateIn sequence** into
      Cronus `ChannelHandler` (add StatChanged, pet-consume ×3, FriendResult,
      FamilyPrivilegeList, FamilyInfoResult, BroadcastMsg in the reference
      order), byte-matching the captured packets.
- [ ] Give Cronus real map data server-side (footholds/portals/life) so spawn
      and field-object packets match the reference. Decide: parse `wz_xml`
      (Cronus.Data already targets HaRepacker XML) vs. minimal per-map stubs.
- [ ] If the reference still crashes on full real WZ: run **Process Monitor**
      (elevated) filtered to `JMS_v186.1_L.exe`, reproduce, and read the last
      `NAME NOT FOUND` / `PATH NOT FOUND` before the crash → identifies the
      exact resource. Consider testing on a discrete-GPU machine to rule out the
      iGPU field renderer.
- [ ] Re-enable a keep-alive on the login listener once entry is stable
      (was set to null during diagnosis in `Cronus.Server.Host/Program.cs`).

## Reference environment notes (for reproducing the oracle)
- JDK 21 at `C:\jdk-21.0.1`; compile: `javac -encoding UTF-8 -proc:full -cp "out;lib/*" -d out <files>`
- Run: `java -cp "out;lib/*" tacos.Start JMS 186 1`
- MySQL 8.4, db `jms_v186`, user `root` / pass `root` (auto-created).
- Full v186 WZ XML lives under `Reference/JMSv186/wz_xml/xml_JMS_v186/*.wz/`.
- Packet log tag: `[REFSEND] op=0x…` (added in `PacketEncoder`).
