# In-Game Commands

> 日本語版: **[COMMANDS.ja.md](COMMANDS.ja.md)**

Chat messages that start with **`/`** are handled by the server as commands — they are **not
broadcast** to other players. Replies are echoed back to you as your own chat line, one chat line
per output row, so multi-line output (`/help`, `/status`) is readable in the client.

Three things the command layer does for you:

- **`/help`** prints the whole set grouped by category, one command per line, and
  **`/help <command>`** prints one command's usage, what it does, and its aliases.
- **Getting the arguments wrong replies with that command's usage** instead of a bare error —
  `/item apple` answers `使い方: /item <アイテムID> [個数]`.
- **A misspelled command suggests the closest real one** — `/healx` answers
  `不明なコマンド: /healx — もしかして /heal ?`.

Every command's metadata lives in one table
([`ChannelHandler.CommandTable.cs`](../src/Cronus.Server.Channel/ChannelHandler.CommandTable.cs)),
which is what keeps `/help`, the usage replies, and this document describing the same thing.

> **No permission system yet.** Every connected player can use every command. That is fine for a
> private in-group server, but keep it in mind before exposing the server more widely.

## Quick reference

### Movement

| Command | Effect |
|---|---|
| `/warp <mapId\|playerName>` | Warp yourself: a number is a map id, a name is that player's map |
| `/dbgwarp` | Windowed warp console — pick a region, an area, then a map (no ids to type) |
| `/pos` | Show your position and map id |
| `/conti state|move <a> [b]` | Send one airship effect packet to yourself (live bisect of the unverified values) |
| `/sweep maps [from] [to] [seconds]` · `/sweep resume [seconds]` · `/sweep stop` | Automated crash inventory: warp through every map; on a crash the last line of `sweep-progress.txt` names the map |
| `/talk <npc id>` | Start that NPC's script conversation here (real effects) |
| `/sweep npcs [from npc] [seconds per page]` | Stream every scripted NPC's dialog to this client (render-crash inventory); on a crash the last `npc` line names it |
| `/gmmove [on|off|<speed×> [<jump×> [<attack×>]]]` | GM movement: speed/jump/attack-speed multipliers, no damage taken, no skill MP cost or cooldown |

### Character

| Command | Effect |
|---|---|
| `/status` | Print your stat sheet |
| `/status <field> <n>` | Change one stat — see the field table below |
| `/heal` | Restore full HP/MP |
| `/maxskills` | Max every skill your current job can learn |
| `/gender [m\|f]` | Toggle (or set) your gender; re-enters the channel to apply the look |
| `/beauty` | Style console: windowed pickers over every hair / hair color / face / eye color / skin |

### Items

| Command | Effect |
|---|---|
| `/item <itemId> [qty]` | Spawn items into your inventory |
| `/drop <itemId\|0> [qty]` | Spawn a ground drop at your feet (`0` = a meso pile) |
| `/shop` | Debug shop: pick a category, then a page — every item costs 1 meso |
| `/shop <shopId>` | Open an NPC shop from the shop table |
| `/storage` | Open your account storage |
| `/clear inv [tab]` | Empty your inventory (all five tabs, or just tab `1`–`5`) |
| `/clear quest <id>` | Clear one quest from your records (started and completed) |
| `/clear book` | Clear your Monster Book, so registered cards drop again |

### World

| Command | Effect |
|---|---|
| `/notice <msg>` | Blue notice broadcast to your current map |
| `/notice all <msg>` | Blue notice broadcast to every map on every channel |
| `/players` | List players online |
| `/guildcreate <name>` | Create a guild (free, works anywhere) with you as master |

### System

| Command | Effect |
|---|---|
| `/save` | Persist your character now |
| `/help [command]` | List the commands, or detail one |

### Aliases

These older spellings still work and are accepted anywhere the canonical name is; `/help` lists
only the canonical form.

| Alias | Canonical |
|---|---|
| `/map <mapId>` | `/warp <mapId>` |
| `/level` `/job` `/exp` `/hp` `/maxhp` `/mp` `/maxmp` `/str` `/dex` `/int` `/luk` `/ap` `/sp` `/fame` `/meso` | `/status <field> <n>` |
| `/snotice <msg>` | `/notice all <msg>` |
| `/clearinv [tab]` | `/clear inv [tab]` |
| `/questreset <id>` | `/clear quest <id>` |
| `/dbgshop` | `/shop` |
| `/online` | `/players` |

## Details

### `/warp <mapId|playerName>`
A number warps you to that map (spawn portal 0); anything else is treated as an online player's
name and warps you to their map. Names are case-insensitive; you get
`'<name>' はオンラインではありません` when the player isn't on this channel (or is yourself).
Map ids work even for maps without wz data (the client draws the map from its own files) — if you
end up somewhere broken, `/warp 100000000` (Henesys) gets you home.
A map id the client has no data for is refused ("マップ … は存在しません") — entering such a map crashes the client.

```
/warp 100010000     ← a low-level hunting map (snails/mushrooms)
/warp Alice         ← jump to Alice
```

### `/dbgwarp`
Opens a windowed warp console instead of asking you to remember ids. It walks
**region → area → map**: the ten regions from the game's own name table
(メイプルアイランド, ビクトリアアイランド, オシリア大陸, …), then the areas ("streets") inside it
(ヘネシス, オルビス, …), then the maps on that street with their ids shown. Lists longer than 20
entries get a page menu first.

Only maps that **actually have field data** are listed. The name table names about 5,500 maps but
only ~3,270 have data in a v186 tree; warping to one of the others would leave the client with
nothing to draw. Requires `CRONUS_WZ` (it reads `String/Map.img.xml` and `Map/`); without it the
command replies that the catalog isn't loaded.

### `/status`
With no arguments it prints your stat sheet:

```
── Hero ── Lv.42 job 112 exp 0
HP 2400/2400   MP 800/800
STR 250  DEX 40  INT 4  LUK 25
AP 0  SP 3  fame 12  meso 1500000
変更するには /status <項目> <値>
```

### `/status <field> <n>`
Changes one stat. Every field also works as a top-level alias (`/hp 500` ≡ `/status hp 500`).

| Field | Effect |
|---|---|
| `level` | **Set** your level (1–200; exp resets to 0) |
| `job` | Set your job id (table below) |
| `exp` | **Set** your exp |
| `hp` · `mp` | **Set** current HP / MP (clamped to the max) |
| `maxhp` · `maxmp` | **Set** max HP / MP (1–30000; pulls the current value down with it) |
| `str` `dex` `int` `luk` | **Set** a base stat (4–32767) |
| `ap` · `sp` | **Add** `n` ability / skill points (clamped to ≥ 0) |
| `fame` | **Set** fame (−30000..30000) |
| `meso` | **Add** `n` meso (negative subtracts; clamped to 0..2,147,483,647) |

`level` is the one with real behaviour behind it: **raising** it runs real level-ups, so HP/MP grow
with your job's ranges (growth passives included) and AP/SP are granted exactly as if you had
hunted for it. **Lowering** just sets the number and leaves your stats alone. Either way exp resets
to 0 and the party window updates. HP/MP changes are pushed to your party's HP bars.

Common pre-Big-Bang job ids (2nd job = base+10; 3rd/4th add +1/+2, e.g. Fighter 110 → Crusader 111
→ Hero 112):

| Base | 1st job | 2nd jobs |
|---|---|---|
| 0 | Beginner | — |
| 100 | Swordman | 110 Fighter / 120 Page / 130 Spearman |
| 200 | Magician | 210 Wizard (F/P) / 220 Wizard (I/L) / 230 Cleric |
| 300 | Archer | 310 Hunter / 320 Crossbowman |
| 400 | Rogue | 410 Assassin / 420 Bandit |
| 500 | Pirate | 510 Brawler / 520 Gunslinger |

### `/heal`
Restores HP and MP to full. Party members see your HP bar update.

### `/maxskills`
Maxes every skill in your current job's learning chain (beginner → 1st job → each advancement up
to your code) at the wz max level. Handy after `/status job` or a fresh advancement.

### `/gender [m|f]`
`/gender` toggles; `/gender m` / `/gender f` (also `male`/`female`, `0`/`1`, `男`/`女`) sets it.
Gender rides in the entry CharacterData, so the client is bounced through a same-channel migration
to redraw the look. The **account's** gender follows too (`GameConstants.GenderCommandChangesAccount`),
which is what the cash shop filters its catalog by — re-login to see the shop switch.

### `/beauty`
Opens the style console: windowed pickers over **every** hair style, hair color, face, eye color,
and skin in the wz data, in pages of avatar previews — no ids to type. Requires `CRONUS_WZ`.

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

### `/drop <itemId|0> [qty]`
Spawns a real ground drop at your feet, so the whole drop → pickup path can be exercised (handy
for client and bot testing). `0` drops a meso pile of `qty` meso instead.

### `/shop` · `/shop <shopId>`
With no id it opens the **debug shop**: pick a category (hats, weapons, use, etc …), then a page,
and every item in that page costs 1 meso. Only items that really have data are stocked — the wz
string tables name ~134 ids a v186 client can't render, and listing one crashes it.

With an id it opens that NPC shop from the shop table (`CRONUS_SHOPS`). Buy with meso; selling pays
the item's wz price. Shop `11100` is a potion shop. (Clicking a vendor NPC on a map opens its shop
too — the command is just the direct route.)

### `/storage`
Opens your account storage (shared by all characters on the account). Deposits cost a flat 100
meso; withdrawals are free.

### `/clear <inv [tab] | quest <id> | book>`
- **`inv`** empties your inventory — all five tabs, or just one with `/clear inv 2`. Only carried
  items go; worn equipment stays. The per-slot removes are sent so the grid clears live.
- **`quest <id>`** clears one quest from both records (started and completed), making a quest flow
  re-runnable — the main use is scripted/bot testing.
- **`book`** clears your Monster Book. Since a mob stops dropping its card once you've registered
  it (`GameConstants.MonsterCardStopDropCount`), this is how you make cards drop again.

### `/notice <msg>` · `/notice all <msg>`
Broadcasts `msg` as the blue system notice — to everyone **in your current map**, or with `all` to
every map on every channel.

### `/players`
Lists the names of everyone online.

### `/guildcreate <name>`
Creates a guild named `name` with you as its master (rank 1) — free, and usable anywhere.
This is the private-server shortcut; the client's own creation flow at the Orbis Guild
Headquarters (map `200000301`) also works and costs the classic 5,000,000 meso. Invite,
join, leave, expel, ranks, emblem (HQ + 15m meso), and notice are all done through the
in-game guild window (G key). The guild master leaving disbands the guild.

### `/save`
Persists your character immediately (it also autosaves periodically and on disconnect). Replies
`saved`.

### `/pos`
Replies with your `(x, y)` position and map id — handy when authoring NPC/portal scripts (see
[SCRIPTING.md](SCRIPTING.md)).

### `/gmmove [on|off|<speed×> [<jump×> [<attack×>]]]`
Toggles GM movement mode (no argument flips it; defaults 3× speed, 1.8× jump, 1× attack).
`/gmmove 5` is 5× speed, `/gmmove 3 2.5 4` is 3× speed, 2.5× jump and 4× attack speed (speed
1–100, jump 1–30, attack 1–8); repeating while on re-applies with the new values. While on: speed
and jump take the multipliers (Speed/Jump temporary stats); attack speed comes from the Booster
stat, solved from the client's frame-time law (base × (degree + 10) / 16) with the equipped
weapon's attackSpeed; hits show their number but take no HP; skills cost no MP/HP and have no
cooldown (the client's own bar prediction is snapped back). A stock client caps speed at 140%,
jump at 123% and attack speed at degree 2; `DevTools\clientpatch_speedcap.py apply` lifts all
three — see [CLIENT_PATCHES.md](CLIENT_PATCHES.md). Re-logging turns it off.

### `/sweep maps [from] [to] [seconds]` · `/sweep resume [seconds]` · `/sweep stop`
Automates the crash inventory. The server warps you through every map it knows (String.wz ∩
Map.wz) in id order, writing the map id and name to the console and to `sweep-progress.txt` next
to the host **before** each warp. When the client crashes, the last line is the culprit;
`/sweep resume` continues from the map after it, `/sweep stop` ends the run. Default dwell is 3 s;
`/sweep maps 100000000 200000000 2` limits the range (about 5,900 maps × 3 s ≈ 5 h, so run it by
region). `python DevTools/wirelog.py` then gives the packet context of the disconnect; each crash
becomes one item in [TASK.md](TASK.md) phase 0.

`/sweep npcs` is the dialog version: the server starts every scripted NPC's conversation (248),
lets this client draw the FIRST page (against a read-only stand-in for the character: no warps,
items or exp), then closes the dialog with a same-map SetField before the next NPC — the client
disconnects when a second script message arrives over an unanswered dialog (seen 2026-09-09), and
only the client can answer a page. 1.5 s per page by default; `/sweep npcs 2000000 1`
sets the first NPC and the pace. Script logic is already covered headless by
`AllScriptsExerciseTests` (every branch, three profiles); this sweep only asks whether the client
can render the pages.

### `/conti state|move <a> [b]`
Sends one airship effect packet to **you only**: `/conti state 4 1` is `LP_CONTISTATE [4][1]`,
`/conti move 10 4` is `LP_CONTIMOVE [10][4]` (states: 1 docked, 2 departing, 3 sailing, 4 enemy
ship appears, 5 enemy ship leaves, 6 end). Use it on a flight map (`/warp 200090010`) to find the
combination that makes the Balrog ship appear; the working values become the server's default.

### `/help [command]`
`/help` lists every command, grouped by category, one per line with its usage.
`/help <command>` details one — its category, usage, what it does, and its aliases. Aliases are
accepted here too, so `/help map` shows the `/warp` entry.

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

(Or just use `/dbgwarp` and pick from the list.)

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
