# Cronus.Debug.Bot — content debugger

A protocol-level content debugger that launches real client windows **and** drives headless
bots against a running Cronus server. Each bot is a genuine encrypted JMS v186 client session
(the same handshake / AES-OFB / framing a real client uses), so from the server's side a bot is
indistinguishable from a player — but it can assert every step and report OK/NG.

## What it does

For each of N bots (default 4), in parallel:

1. **Launches a real client window** (the localhost-patched `JMS_v186.1_L.exe`) so the content is
   also visible on screen for a human to watch or play. Best-effort: skipped off-Windows or when
   the client isn't found; the bots still run.
2. **Runs the headless content walkthrough** — the real login flow (auto-registering
   `cronusbot{N}`), character create/select, game entry, then: `/help`, `/meso`+`/level`,
   `/item`, movement, a taxi NPC dialog, the **salon style-picker** (asserts the `askAvatar`
   UI opens), a self-whisper, the **Zakum door** dialog, a full **cash-shop round trip**
   (enter → buy a catalog item into the locker → return to the channel), and an
   **in-game channel change**.
3. **Paired scenarios** between the first two bots: a **cross-bot whisper** and a
   **party invite/accept**.

At the end it prints a per-bot `OK/NG` report and exits non-zero if anything failed.

## Running it

Start a server first (see [SERVER_SETUP.md](SERVER_SETUP.md)), then:

```powershell
dotnet run --project src/Cronus.Debug.Bot            # 4 bots + 4 client windows vs 127.0.0.1:8484
dotnet run --project src/Cronus.Debug.Bot -- 8       # 8 of each
```

| Env var | Effect |
|---|---|
| `CRONUS_BOT_COUNT` | Number of bots / client windows (default 4; arg 1 overrides). |
| `CRONUS_BOT_HOST` / `CRONUS_BOT_PORT` | Login endpoint (default `127.0.0.1:8484`; arg 2 overrides host). |
| `CRONUS_BOT_LAUNCH` | `0` = don't launch real client windows (bots only). |
| `CRONUS_CLIENT_PATH` | Explicit path to the client exe; otherwise the bundled `Client/MapleStory_v186/JMS_v186.1_L.exe` is auto-detected. |

## Scope / limits

- The real client windows are for **visibility** — a human watches or plays them. The bots are
  separate connections; the closed, DirectX-rendered client can't be reliably driven by key/mouse
  automation (NPC clicks, menu navigation, reading results), so content **verification** is the
  bots' job, not the windows'.
- Anti-cheat protects the client process: once launched it can't be force-killed from a script —
  close the windows manually.
- The bots exercise the server's real code paths; they found and pinned at least one server
  robustness bug (a short script answer over-reading and dropping the session).
