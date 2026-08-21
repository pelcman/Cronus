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

- One PC (yours) runs the **server**. It listens on two TCP ports (login + channel).
- You open those ports so friends can reach your PC's public IP.
- Each friend runs a **JMS v186 client** pointed at your IP, with a small WZ patch
  applied (fixes the game-entry crash). They log in and play together.

```
 friend's PC ──(internet)──▶ your public IP :8484 (login)
                                            :7575 (channel)
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
- *(Optional but recommended)* **MySQL 8** if you want accounts/characters to persist
  across restarts. Without it, the server keeps everything in memory (wiped on
  restart) — fine for a first test.

**On each player's PC (you and friends):**
- A **JMS v186** client (the exact 1.86 version).
- The **WZ patch** applied to the client (see Part 4). Without it the client crashes
  when entering a map.
- **EmuClient** (https://github.com/Riremito/EmuClient) to point the client at the
  server's IP and bypass the client's version/CRC checks.

---

## Part 1 — Build and first run (local test)

From the repo root:

```powershell
dotnet build Cronus.slnx -c Release
dotnet run --project src/Cronus.Server.Host        # login 8484, channel 7575
```

You should see:

```
Cronus — JMS v186, region Jms
  login   : listening on 0.0.0.0:8484
  channel : listening on 0.0.0.0:7575, advertised to clients as 127.0.0.1:7575
  (localhost only — set CRONUS_HOST=<your LAN/public IP> so friends can connect)
Accounts auto-register on first login. Press Ctrl+C to stop.
```

Confirm it works locally first (Part 4/5 with `127.0.0.1`) before going remote.

---

## Part 2 — Configure it for friends

The server is configured entirely through **environment variables**. The one that
matters most for remote play is `CRONUS_HOST`.

| Variable | What it does | Example |
|---|---|---|
| `CRONUS_HOST` | **The IP the server tells clients to use for the channel.** Set this to your **public IP** (or LAN IP for a LAN party). If unset it's `127.0.0.1` = localhost only. | `203.0.113.9` |
| `CRONUS_DB` | MySQL connection string → persistent accounts/characters. Unset = in-memory (wiped on restart). | `server=localhost;database=cronus;user=root;password=...` |
| `CRONUS_WZ` | Path to a `wz_xml` data tree → NPC/mob/portal spawns. Unset = empty maps (you can still walk around; the client draws the map from its own wz). | `data/sample-wz` |
| `CRONUS_SCRIPTS` | Script root (`{root}/npc/{id}.js`, `{root}/portal/{name}.js`) → NPC dialogs and portal scripts. | `scripts` |
| `CRONUS_DROPS` | Path to a `drop_data.sql` dump → mobs drop items/meso from their drop tables. Unset = mobs drop a small placeholder meso pile only. | `drop_data.sql` |
| `CRONUS_SHOPS` | Path to a `shops`+`shopitems` SQL dump (e.g. `init_data_set.sql`) → vendor NPCs open shops to buy/sell. Unset = shops disabled. | `init_data_set.sql` |
| `CRONUS_STARTMAP` | Map new characters spawn in. | `100000000` |
| *(args)* | `dotnet run --project src/Cronus.Server.Host <loginPort> <channelPort>` overrides the ports. | `8484 7575` |

Find your **public IP** by visiting e.g. https://ifconfig.me from the server PC.
Then, for example (PowerShell):

```powershell
$env:CRONUS_HOST = "203.0.113.9"     # <-- your public IP
# $env:CRONUS_DB = "server=localhost;database=cronus;user=root;password=YOURPW"
dotnet run --project src/Cronus.Server.Host
```

The startup log should now show `advertised to clients as 203.0.113.9:7575`.

> Note: a home public IP usually changes over time. For a stable address use a free
> **Dynamic DNS** hostname (e.g. DuckDNS) — `CRONUS_HOST` accepts a hostname too
> (it's resolved to IPv4).

---

## Part 3 — Open the ports

Friends' clients must reach **both** ports on your public IP: **8484** (login) and
**7575** (channel), TCP.

1. **Windows Firewall** on the server PC: allow inbound TCP 8484 and 7575
   (Control Panel → Windows Defender Firewall → Advanced → Inbound Rules → New Rule →
   Port → TCP → `8484,7575` → Allow).
2. **Router port-forwarding**: forward external TCP 8484 and 7575 to your server PC's
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

> The sample client in this workspace (`Client/MapleStory_v186/JMS_v186.1_L.exe`) is
> pre-patched to connect to `127.0.0.1` only — good for the operator's local test, not
> for friends. Friends use EmuClient with `LocalHost.ini` set to your IP.
>
> TODO (finalize when we test remote play): document EmuClient `LocalHost.ini` exactly
> and confirm the channel handoff works across the internet end-to-end.

---

## Part 5 — Play

1. **Login** — any ID/password; unknown accounts **auto-register** on first login.
2. **World / channel** — one world ("Cronus"), two channels.
3. **Character** — create one (starter gear is stored and rendered).
4. **Enter game** — spawns in `CRONUS_STARTMAP` (default `100000000`). With the WZ
   patch applied this now works.
5. **Play together** — move, chat, fight mobs, pick up drops, use portals. Other
   players in the same map appear and their movement/chat is relayed.

GM/debug chat: `!map <id>`, `!meso <n>`, `!notice <msg>`, `!pos`, `!help`.

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
- **Accounts/characters vanish after restart** → you're on in-memory storage; set
  `CRONUS_DB` (Part 2) to a MySQL connection string.
- **Deeper packet issues** → see [VALIDATION.md](VALIDATION.md) and compare bytes with
  [RirePE](https://github.com/Riremito/RirePE) against the Java reference server.

---

## Configuration quick reference

```powershell
# Minimal local test (in-memory, localhost)
dotnet run --project src/Cronus.Server.Host

# Friends over the internet, persistent, with map/NPC data
$env:CRONUS_HOST    = "203.0.113.9"     # your public IP or a DDNS hostname
$env:CRONUS_DB      = "server=localhost;database=cronus;user=root;password=YOURPW"
$env:CRONUS_WZ      = "data/sample-wz"
$env:CRONUS_STARTMAP= "100000000"
dotnet run --project src/Cronus.Server.Host 8484 7575
```
