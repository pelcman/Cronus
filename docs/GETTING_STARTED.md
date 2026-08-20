# Getting started: connecting a real JMS v186 client

This walks through running Cronus and connecting an actual JMS v186 client to it, locally.

> ⚠️ Local / research / educational use only. Cronus bundles no client or copyrighted game
> data. You supply your own JMS v186 client.

## 1. Run the server

```powershell
# From the repo root
dotnet run --project src/Cronus.Server.Host          # login 8484, channel 7575
# or pick ports:  dotnet run --project src/Cronus.Server.Host 8484 7575
```

You should see:

```
Cronus — JMS v186, region Jms
  login   : 0.0.0.0:8484
  channel : 0.0.0.0:7575
Accounts auto-register on first login. Press Ctrl+C to stop.
```

With nothing else configured the server uses in-memory storage and has no map/NPC data (you
can still log in, create a character, enter the game, and walk around — the client renders the
map from its own wz files). To enable persistence, maps/NPCs/mobs, and scripts, set:

| Env var | Effect |
|---|---|
| `CRONUS_DB` | MySQL connection string → persistent accounts/characters/items |
| `CRONUS_WZ` | a wz_xml tree → NPC/mob/portal spawns (try the bundled `data/sample-wz`) |
| `CRONUS_SCRIPTS` | script root (`{root}/npc/{id}.js`) → NPC dialogs (try `scripts`) |

```powershell
$env:CRONUS_WZ = "data/sample-wz"
$env:CRONUS_SCRIPTS = "scripts"
dotnet run --project src/Cronus.Server.Host
```

New characters start in map `100000000`; with the sample wz that map has a talkable NPC
(9010000) wired to [scripts/npc/9010000.js](../scripts/npc/9010000.js).

## 2. Point a JMS v186 client at it

The client must be redirected to `127.0.0.1` and have its version/CRC checks bypassed. Use
[EmuClient](https://github.com/Riremito/EmuClient) (verified on JMS v164/165/186/188/194):

1. Get a **JMS v186** client.
2. Configure EmuClient's `LocalHost.ini` to redirect the login server to `127.0.0.1:8484`
   (the login port above).
3. Launch the client through EmuClient (`RunEmu` / `RunEmu64`), which injects the loader that
   applies the localhost redirect and the MSCRC bypass. For v187 and below, EmuClient's
   `iGPUplz` helper is recommended for graphics/startup.

## 3. What should happen

1. **Login screen** — enter any ID/password. Unknown accounts **auto-register** on first login,
   so the first login creates the account. (This confirms the whole network core: handshake,
   AES-OFB crypto, framing, opcode table, and `LP_CheckPasswordResult`.)
2. **World / channel select** — one world ("Cronus") with two channels.
3. **Character select** — empty at first; **create a character** (its starter equipment is
   stored and rendered).
4. **Enter game** — the character spawns in map `100000000`. With `CRONUS_WZ` set, the sample
   NPC and any mobs appear; click the NPC to run its script.
5. **Play** — move, chat, use portals/`!map <id>`, and (with a mob present) attack it: it takes
   damage, dies, grants exp (levels you up), and drops meso you can pick up. Spend SP on skills.

GM/debug chat commands: `!map <id>`, `!meso <n>`, `!notice <msg>`, `!pos`, `!help`.

## 4. If something doesn't work

The server's packet layouts are reverse-engineered from
[Riremito/JMSv186](https://github.com/Riremito/JMSv186) and covered by round-trip tests, but
have **not yet been byte-validated against the real client**. If the client stalls or
disconnects at a step, that step's packet is the prime suspect. Use
[RirePE](https://github.com/Riremito/RirePE) to compare Cronus' bytes for that opcode against
the Java JMSv186 server (the ground truth) and fix the first differing field. See
[VALIDATION.md](VALIDATION.md) for the full validation workflow and the implemented-packet
checklist.

Common first checks:
- **Stuck on the login/loading screen** → the Hello handshake or `LP_CheckPasswordResult`
  layout. Confirm the client is really hitting `127.0.0.1:8484` (EmuClient redirect) and that
  the client is exactly **v186**.
- **Disconnects on character select** → `LP_SelectWorldResult` / character-list encoding.
- **Disconnects on entering the game** → the `LP_SetField` CharacterData blob (the largest,
  most conditional structure).
- **Idle disconnects** → the server pings every 15s (`LP_AliveReq`); if the client still drops,
  check that pings are being sent/acked in the RirePE log.
