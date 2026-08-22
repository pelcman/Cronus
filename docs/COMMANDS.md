# In-Game Commands

> 日本語版: **[COMMANDS.ja.md](COMMANDS.ja.md)**

Chat messages that start with **`/`** are handled by the server as commands — they are **not
broadcast** to other players. Replies are echoed back to you as your own chat line. An unknown
command replies `unknown command: <name>`.

> **No permission system yet.** Every connected player can use every command. That is fine for a
> private in-group server, but keep it in mind before exposing the server more widely.

Characters live in memory unless `CRONUS_DB` is set, so anything you grant with commands is wiped
when the server restarts (see [SERVER_SETUP.md](SERVER_SETUP.md)).

## Quick reference

| Command | Effect |
|---|---|
| `/map <mapId>` | Warp yourself to a map |
| `/warp <playerName>` | Warp yourself to another player's map |
| `/meso <n>` | Add `n` meso (negative subtracts) |
| `/heal` | Restore full HP/MP |
| `/job <jobId>` | Set your job |
| `/level <n>` | **Set** your level (1–200; exp resets to 0) |
| `/hp <n>` · `/mp <n>` | **Set** current HP / MP (clamped to the max) |
| `/maxhp <n>` · `/maxmp <n>` | **Set** max HP / MP (1–30000) |
| `/str /dex /int /luk <n>` | **Set** a base stat (4–32767) |
| `/ap <n>` | Add `n` ability points |
| `/sp <n>` | Add `n` skill points |
| `/fame <n>` | **Set** fame to `n` |
| `/item <itemId> [qty]` | Spawn items into your inventory |
| `/shop <shopId>` | Open an NPC shop |
| `/storage` | Open your account storage |
| `/save` | Persist your character now |
| `/players` (alias `/online`) | List players online on this channel |
| `/guildcreate <name>` | Create a guild (free, works anywhere) with you as master |
| `/maxskills` | Max every skill your current job can learn |
| `/questreset <id>` | Clear one quest from your records (started and completed) |
| `/gender [m\|f]` | Toggle (or set) your character's gender (account gender follows for the cash shop — re-login to see its catalog switch); re-enters the channel to apply the look |
| `/beauty` | Open the style console: windowed pickers over every hair style / hair color / face / eye color / skin |
| `/dbgshop` | Open the debug shop: pick a category (hats, weapons, use, etc …), then a page — every item costs 1 meso |
| `/notice <msg>` | Blue notice broadcast to your current map |
| `/snotice <msg>` | Blue notice broadcast to the whole server |
| `/pos` | Show your position and map id |
| `/help` | List the commands |

## Details

### `/map <mapId>`
Warps you to the map with that id (spawn portal 0). Works even for maps without wz data (the
client draws the map from its own files). If you end up somewhere broken, warp back with
`/map 100000000` (Henesys).

```
/map 100010000     ← a low-level hunting map (snails/mushrooms)
```

### `/warp <playerName>`
Warps you to the map another online player is in. Names are case-insensitive; replies
`'<name>' is not online` when the player isn't on this channel (or is yourself).

### `/meso <n>`
Adds `n` meso to your wallet (clamped to 0..2,147,483,647). Negative values subtract.

### `/heal`
Restores HP and MP to full. Party members see your HP bar update.

### `/job <jobId>`
Sets your job id. Common pre-Big-Bang ids (2nd job = base+10; 3rd/4th job add +1/+2, e.g. Fighter
110 → Crusader 111 → Hero 112):

| Base | 1st job | 2nd jobs |
|---|---|---|
| 0 | Beginner | — |
| 100 | Swordman | 110 Fighter / 120 Page / 130 Spearman |
| 200 | Magician | 210 Wizard (F/P) / 220 Wizard (I/L) / 230 Cleric |
| 300 | Archer | 310 Hunter / 320 Crossbowman |
| 400 | Rogue | 410 Assassin / 420 Bandit |
| 500 | Pirate | 510 Brawler / 520 Gunslinger |

### `/level <n>`
Raising your level runs **real level-ups** — HP/MP grow with your job's ranges (growth passives
included) and AP/SP are granted, exactly like levelling by hunting. Lowering just sets the level
(stats keep their values). Exp resets to 0 so the bar starts clean; the party window updates.

### `/hp <n>` · `/mp <n>` · `/maxhp <n>` · `/maxmp <n>`
**Set** the current or maximum HP/MP. Current values clamp to the max (setting `/hp 0` kills you —
handy for testing death/revive); max values clamp to 1–30000 and pull the current value down with
them. Party members see HP changes.

### `/str <n>` · `/dex <n>` · `/int <n>` · `/luk <n>`
**Set** a base stat directly (4–32767). The client recomputes derived values (damage, accuracy…)
from the new stat.

### `/ap <n>` · `/sp <n>`
Add `n` ability / skill points (deltas; the result is clamped to ≥ 0). Spend SP in the skill
window (default key `K`) — skill levels are capped at the wz max.

### `/fame <n>`
**Sets** (not adds) your fame to `n`, clamped to −30000..30000.

### `/item <itemId> [qty]`
Spawns `qty` (default 1) of an item into your inventory. Bundles stack up to the item's wz
`slotMax`; equips take one slot each and receive their wz base stats (attack, defense, upgrade
slots …), so you can drag them onto an equip slot right away.

```
/item 2000000 30   ← 30 Red Potions (HP +50)
/item 1302000      ← a Sword (17 atk, 7 upgrade slots)
/item 2030004      ← Return-to-Henesys scroll (use it to warp)
/item 2012000      ← attack potion (watk +8 buff for 5 min)
```

### `/shop <shopId>`
Opens the NPC shop with that id from the shop table (`CRONUS_SHOPS`). Buy with meso; selling pays
the item's wz price. Shop `11100` is a potion shop. (Clicking a vendor NPC on a map opens its shop
too — the command is just the direct route.)

### `/storage`
Opens your account storage (shared by all characters on the account). Deposits cost a flat 100
meso; withdrawals are free. In-memory for now — cleared on server restart.

### `/save`
Persists your character immediately (it also autosaves periodically and on disconnect). Replies
`saved`.

### `/players` (alias `/online`)
Lists the names of everyone online on this channel.

### `/guildcreate <name>`
Creates a guild named `name` with you as its master (rank 1) — free, and usable anywhere.
This is the private-server shortcut; the client's own creation flow at the Orbis Guild
Headquarters (map `200000301`) also works and costs the classic 5,000,000 meso. Invite,
join, leave, expel, ranks, emblem (HQ + 15m meso), and notice are all done through the
in-game guild window (G key). The guild master leaving disbands the guild.

### `/maxskills`
Maxes every skill in your current job's learning chain (beginner → 1st job → each advancement up
to your code) at the wz max level. Handy after `/job` or a fresh advancement.

### `/notice <msg>`
Broadcasts `msg` as the blue system notice to everyone **in your current map**.

### `/pos`
Replies with your `(x, y)` position and map id — handy when authoring NPC/portal scripts (see
[SCRIPTING.md](SCRIPTING.md)).

### `/help`
Lists all commands in one line.

## Client built-in commands

The client's own slash commands keep working independently of the server command set — most
usefully the whisper UI's **`/find <name>`** (location lookup) and whispering itself, both served
by the server's whisper handler.

## Handy ids

Towns: `100000000` Henesys · `101000000` Ellinia · `102000000` Perion · `103000000` Kerning City ·
`104000000` Lith Harbor · `120000000` Nautilus.
Hunting maps near Henesys: `100010000`, `100020000`, `100030000`.
Free Market rooms (personal shops / hired merchants): `910000001`–`910000022`.
Guild HQ (client-side guild creation / emblem): `200000301` (Orbis).

Fun items to `/item` yourself:

| Id | Item |
|---|---|
| `4080000`–`4080004` | Omok stone sets (open an Omok room) |
| `4080100` | Match-card (神経衰弱) set |
| `5140000` | Store permit (露店, personal shop; FM rooms only) |
| `5030000` | Employee permit (雇用商人; FM rooms only) |
| `5000000`+ | Pets (double-click in the cash tab to summon) |
| `2120000` | Pet food |
| `5070000` / `5071000` | Megaphone / Super Megaphone |
| `5073000` / `5074000` | Heart / Skull Megaphone |
| `5370000` | Ad board (黒板) |
| `3010000`+ | Portable chairs |
| `2340000` | White Scroll (protects an upgrade slot on scroll failure) |
| `2049000` / `2049100` | Clean Slate Scroll / Chaos Scroll |
