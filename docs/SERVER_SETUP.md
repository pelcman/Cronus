# Hosting a Cronus server for friends

This is the step-by-step guide to running a Cronus (JMS v186) server that your
friends can join over the internet on a **fixed IP**. It's written for someone with
only basic PC knowledge. For a quick *localhost-only* try-out first, see
[GETTING_STARTED.md](GETTING_STARTED.md).

> ⚠️ **Private, in-group use only.** MapleStory is Nexon's property; a public or
> commercial server carries real legal risk in Japan. Keep this to you and friends,
> for hobby/research/educational use. Cronus ships no client and no game data — each
> player supplies their own JMS v186 client.

---

## What you'll end up with

- One PC (yours) runs the **server**. It listens on a login port plus one port per game
  channel (default 2 channels).
- You open those ports so friends can reach your PC's public IP.
- Each friend runs a **JMS v186 client** pointed at your IP, with a small WZ patch
  applied (fixes the game-entry crash). They log in and play together.

```
 friend's PC ──(internet)──▶ your public IP :8484 (login)
                                            :7575 :7576 (channels)
                                     ▲
                                  Cronus server (your PC)
```

---

## Part 0 — Prerequisites

**On the server PC (yours):**
- Windows/Linux/macOS with the **.NET SDK 10** installed
  (check: `dotnet --version` prints `10.x`). Get it from
  https://dotnet.microsoft.com/download .
- The Cronus source (this repo).
- **MySQL 8** (required). The server keeps accounts, characters, items, storage, guilds,
  merchants and parcels in one database, **`Cronus186`**, which it creates on first start
  (defaults: `127.0.0.1:3306`, user `root`, password `root`; `setup.bat` asks). Without MySQL
  the server refuses to start; `CRONUS_DB=sqlite` is the single-file fallback for a quick test.

**On each player's PC (you and friends):**
- A **JMS v186** client (the exact 1.86 version).
- The **WZ patch** applied to the client (see Part 4). Without it the client crashes
  when entering a map.
- **EmuClient** (https://github.com/Riremito/EmuClient) to point the client at the
  server's IP and bypass the client's version/CRC checks.

---

## Part 1 — Build and first run (local test)

**The short way (Windows):** double-click **`setup.bat`** in the repo root. It checks the
.NET SDK, creates `.env`, asks for your client folder (or drag the folder onto the script),
builds everything, and ingests the client's `.wz` files into `gamedata.db`. When it finishes,
`run-server.bat` starts the server. Re-run **`ingest.bat`** whenever the client's data files
change (the old database is simply replaced — stop the server first).

By hand, from the repo root:

```powershell
dotnet build Cronus.slnx -c Release
dotnet run --project src/Cronus.Ingest -- <client dir>   # -> gamedata.db (once)
run-server.bat                                           # World + Login + Channel
```

(`run-server.bat` = `dotnet run --project src/Cronus.Server.World`, then `…Login`, then
`…Channel`, each from the repo root; `stop-server.bat` stops all three.) You should see, in
the Channel window:

```
[world] registered with http://127.0.0.1:8585 as PC-1234: channel(s) 1, 2, heartbeat every 5s
Cronus Channel — JMS v186, region Jms
  channels: 2 — ports 7575..7576, advertised to clients as 127.0.0.1
  (localhost only — set CRONUS_HOST=<your LAN/public IP> so friends can connect)
```

The World's gRPC port (8585) is loopback-only and is **not** opened to the internet.

Confirm it works locally first (Part 4/5 with `127.0.0.1`) before going remote.

---

## Part 2 — Configure it for friends

The server is configured through **environment variables**, and the easiest way to set
them is a **`.env` file at the repo root** (the Maple2 approach): copy
[`.env.example`](../.env.example) to `.env`, edit, done — the server and the debug bot
load it at startup (`[env] loaded …` in the log), and any variable set in the real
environment overrides the file. The one that matters most for remote play is
`CRONUS_HOST`.

| Variable | What it does | Example |
|---|---|---|
| `CRONUS_HOST` | **The IP the server tells clients to use for the channel.** Set this to your **public IP** (or LAN IP for a LAN party). If unset it's `127.0.0.1` = localhost only. | `203.0.113.9` |
| `CRONUS_DB` | Storage mode. **Unset = MySQL** built from the `CRONUS_DB_*` settings below. A full MySQL connection string, `sqlite` (one file next to the exe, or `CRONUS_DB_FILE`) or `memory` (wiped on exit) override it. | `sqlite` |
| `CRONUS_DB_HOST` / `CRONUS_DB_PORT` / `CRONUS_DB_NAME` / `CRONUS_DB_USER` / `CRONUS_DB_PASSWORD` | The MySQL connection (defaults `127.0.0.1` / `3306` / `Cronus186` / `root` / `root`). The database is created on first start. | `CRONUS_DB_PASSWORD=secret` |
| `CRONUS_DB_FILE` | Path of the SQLite file for `CRONUS_DB=sqlite`, and of the old save that is imported once into an empty MySQL database (then renamed `.imported`). | `D:\cronus\save.db` |
| `CRONUS_GAMEDATA` | **The game-data database** (`gamedata.db`) built from a client's `.wz` files by `Cronus.Ingest` — one file carrying every map/mob/NPC/item/quest/string definition, guaranteed identical to what the players' client renders. Build it once: `dotnet run --project src/Cronus.Ingest -- <client dir>`. | `gamedata.db` |
| `CRONUS_CLIENT` | Alternative to the above: point at the **client folder** itself. On first boot the server builds `gamedata.db` from it automatically (~20s) and reuses it afterwards. | `C:\...\MapleStory_v186` |
| `CRONUS_WZ` | Legacy fallback: a loose `wz_xml` dump tree, used only when neither of the above is set. Unset = empty maps (you can still walk around; the client draws the map from its own wz). | `data/sample-wz` |
| `CRONUS_SCRIPTS` | Script root (`{root}/npc/{id}.js`, `{root}/portal/{name}.js`) → NPC dialogs and portal scripts. | `scripts` |
| `CRONUS_DROPS` | Path to a `drop_data.sql` dump → mobs drop items/meso from their drop tables. Unset = mobs drop a small placeholder meso pile only. | `drop_data.sql` |
| `CRONUS_SHOPS` | Path to a `shops`+`shopitems` SQL dump (e.g. `init_data_set.sql`) → vendor NPCs open shops to buy/sell. Unset = shops disabled. | `init_data_set.sql` |
| `CRONUS_RATE_EXP` / `CRONUS_RATE_DROP` / `CRONUS_RATE_MESO` | Server rate multipliers for kill exp, drop chance, and mob meso. Unset = 1.0 (authentic). | `4` |
| `CRONUS_AUTO_REGISTER` | `0` / `false` disables account auto-creation, so **only pre-existing accounts can log in** (a simple allow-list for a public IP). Unset/`1` = unknown accounts are created on first login. | `0` |
| `CRONUS_CHANNELS` | How many game channels to run (1–8, default 2). Channels listen on **consecutive ports** from the channel port (7575, 7576, …) — open/forward all of them. In-game channel change works between them. | `2` |
| `CRONUS_NX` | The cash-shop allowance: each account is topped up to this NX floor when entering the shop. The cash-shop server listens on the **next port after the channels** (7577 with 2 channels) — open/forward it too. `0` disables the shop. | `300000` |
| `CRONUS_STARTMAP` | Map new characters spawn in. | `100000000` |
| `CRONUS_LOGIN_PORT` / `CRONUS_CHANNEL_PORT` | The Login's port and the first channel port (also the first argument of `…Server.Login` / `…Server.Channel`). | `8484` / `7575` |
| `CRONUS_WORLD_PORT` / `CRONUS_WORLD_BIND` / `CRONUS_WORLD_URI` | Where the World listens (loopback:8585 by default) and where the other two processes find it. Only change these when the processes run on different machines. | `8585` |

Find your **public IP** by visiting e.g. https://ifconfig.me from the server PC.
Then edit `.env`:

```ini
CRONUS_HOST=203.0.113.9     # <-- your public IP
```

and run the server (PowerShell alternative: `$env:CRONUS_HOST = "..."` before the run):

```powershell
run-server.bat
```

The Channel window should now show `advertised to clients as 203.0.113.9`.

> Note: a home public IP usually changes over time. For a stable address use a free
> **Dynamic DNS** hostname (e.g. DuckDNS) — `CRONUS_HOST` accepts a hostname too
> (it's resolved to IPv4).

---

## Part 3 — Open the ports

Friends' clients must reach the login port **8484**, **every channel port**, and the
**cash-shop port** on your public IP — with the default 2 channels that's
**7575, 7576, and 7577**, TCP.

1. **Windows Firewall** on the server PC: double-click **`port_open.bat`** in the repo root.
   It reads `CRONUS_CHANNELS` from your `.env`, works out the exact ports, asks for
   administrator rights, and creates a single inbound TCP rule named `Cronus JMSv186`.
   Re-run it after changing the channel count — it replaces the old rule. **`port_close.bat`**
   removes it again when you are done hosting.
   (By hand: Control Panel → Windows Defender Firewall → Advanced → Inbound Rules → New Rule →
   Port → TCP → `8484,7575-7577` → Allow.)
2. **Router port-forwarding**: forward external TCP 8484 and 7575-7577 to your server PC's
   **LAN IP** (find it with `ipconfig`). This is done in your router's admin page
   (search "port forwarding" for your router model).
3. **Test** from outside your network (e.g. a friend, or a phone on mobile data):
   `Test-NetConnection <your-public-ip> -Port 8484` should succeed.

---

## Part 4 — Prepare each player's client

Every player (you included) does this once, to their own JMS v186 client:

1. **Apply the WZ patch** (fixes the game-entry crash — the client otherwise crashes
   with error `0x80030002` when entering a map). Close the client, then:
   ```
   python DevTools\wzpatch_namespace.py apply
   ```
   (adjust the `CLIENT` path at the top of the script to their client folder). This
   flips two bytes in `NameSpace.dll` — the same change as Riremito's *iGPUplz*
   "gfx fix (pre-bb)". See root `CLIENT_RENDERING_FIX.md` for the why.
2. **Point the client at the server** and bypass its version/CRC checks with
   **EmuClient**:
   - Set EmuClient's `LocalHost.ini` to redirect the login server to
     **`<server-public-ip>:8484`** (for the server operator testing locally, use
     `127.0.0.1:8484`).
   - Launch the client through EmuClient's `RunEmu` / `RunEmu64`.

> **EmuClient, exactly (verified 2026-09-03).** In the client folder, two INI files matter —
> both must keep **CRLF** line endings (the Win32 INI reader silently returns empty values
> on LF-only files):
>
> ```ini
> ; LocalHost.ini — every connection the client makes is rewritten to this address
> [LocalHost]
> ServerIP=219.100.161.153      ; <-- the server's public IP (LAN IP for a LAN party)
> AuthHook=0
> FixedPortNumber=0
>
> ; RunEmu.ini — what RunEmu launches; TargetEXE must point INSIDE this client folder
> [RunEmu]
> TargetEXE=C:\path	o	his\client\JMS_v186.1_L.exe
> LoaderDLL=EmuLoader.dll
> ```
>
> Launch with `RunEmu.exe` (it self-elevates). Because the redirect is in `LocalHost.ini`,
> friends never need a re-patched exe — one line per PC. The channel handoff and the
> cash-shop hop follow the server's **advertised** address, so `CRONUS_HOST` on the server
> must be the same public IP: with it left at `127.0.0.1`, login succeeds but entering the
> game sends the client to its own PC and drops. Rehearsed on 2026-09-03 with the server
> advertising a non-loopback address and all bot steps (channel change, cash-shop round
> trip) connecting through it.

---

## Part 5 — Play

1. **Login** — any ID/password; unknown accounts **auto-register** on first login.
2. **World / channel** — one world ("Cronus"), two channels.
3. **Character** — create one (starter gear is stored and rendered).
4. **Enter game** — spawns in `CRONUS_STARTMAP` (default `100000000`). With the WZ
   patch applied this now works.
5. **Play together** — move, chat, fight mobs, pick up drops, use portals. Other
   players in the same map appear and their movement/chat is relayed.

In-game commands use the `/` prefix — `/warp <mapId|player>`, `/dbgwarp`, `/status <field> <n>`, `/help`, … — see [COMMANDS.md](COMMANDS.md) ([日本語](COMMANDS.ja.md)). `/help` lists everything, and getting a command's arguments wrong replies with its usage.

---

## Troubleshooting

- **Friend can't reach the login screen** → ports not open. Re-check Part 3
  (firewall + router forwarding on **both** 8484 and 7575); test with
  `Test-NetConnection`.
- **Login works but "entering game" hangs/drops for a remote friend** → the channel
  IP. Make sure `CRONUS_HOST` is your **public** IP (the log line "advertised to
  clients as …") and that **7575** is forwarded too, not just 8484.
- **Client crashes entering a map with `0x80030002`** → the WZ patch isn't applied to
  that client (Part 4, step 1).
- **Client won't start / "不正なプログラム"** → don't use DLL-injection tools
  (dgVoodoo2 etc.); this client's anti-cheat rejects them. The WZ patch is a safe
  2-byte edit and is fine.
- **Accounts/characters vanish after restart** → you set `CRONUS_DB=memory`, or the
  startup log shows a `[db] SQLite unavailable`/`MySQL unavailable` fallback — check
  that line. The default (no `CRONUS_DB`) persists to `cronus.db` automatically.
- **Deeper packet issues** → see [VALIDATION.md](VALIDATION.md) and compare bytes with
  [RirePE](https://github.com/Riremito/RirePE) against the Java reference server.

---

## Backing up the save data

Everything (accounts, characters, items, storage, guilds, merchants, parcels) lives in the
MySQL database `Cronus186`. Back it up with `mysqldump`, restore with `mysql`:

```
mysqldump -uroot -p Cronus186 > cronus186-backup.sql
mysql     -uroot -p Cronus186 < cronus186-backup.sql
```

A server upgrade never needs a fresh database: new tables and columns are added in place
on start. If you still have a `cronus.db` from before 2026-09-08, the first MySQL start imports
it (all keys kept) and renames it `cronus.db.imported`.

---

## Configuration quick reference

```powershell
# Windows: setup.bat once (SDK check + .env + build + client ingest), then
#          run-server.bat to play; ingest.bat rebuilds gamedata.db after a
#          client change; port_open.bat / port_close.bat manage the firewall

# Minimal local test (MySQL Cronus186 on 127.0.0.1, localhost clients)
run-server.bat                                   # = World, Login, Channel

# Friends over the internet, with map/NPC data
$env:CRONUS_HOST    = "203.0.113.9"     # your public IP or a DDNS hostname
$env:CRONUS_WZ      = "data/sample-wz"
$env:CRONUS_STARTMAP= "100000000"
run-server.bat

# Optional: a SQLite file (no MySQL on this machine) or pure memory (throwaway tests)
# $env:CRONUS_DB = "server=localhost;database=cronus;user=root;password=YOURPW"
# $env:CRONUS_DB = "memory"
```
