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
| `/ap <n>` | Add `n` ability points |
| `/sp <n>` | Add `n` skill points |
| `/fame <n>` | **Set** fame to `n` |
| `/item <itemId> [qty]` | Spawn items into your inventory |
| `/shop <shopId>` | Open an NPC shop |
| `/storage` | Open your account storage |
| `/save` | Persist your character now |
| `/players` (alias `/online`) | List players online on this channel |
| `/notice <msg>` | Blue notice broadcast to your current map |
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
