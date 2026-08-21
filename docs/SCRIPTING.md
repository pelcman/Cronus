# Cronus scripting guide (NPC & portal scripts)

Cronus runs game scripts on [Jint](https://github.com/sebastienros/jint) (a C# JavaScript engine),
so NPC dialogs and special portals are plain `.js` files — no rebuild needed to add or change them.
Point the server at a script folder with the `CRONUS_SCRIPTS` environment variable:

```
CRONUS_SCRIPTS/
  npc/
    9010000.js        # one file per NPC template id
    1012100.js
  portal/
    enterDungeon.js   # one file per portal `script` name (from the map wz)
```

Both kinds of script define a `function start() { … }` that runs when the player interacts.
Scripts are loaded on demand and cached.

---

## NPC scripts — `CRONUS_SCRIPTS/npc/<npcId>.js`

Run when a player clicks the NPC whose **template id** matches the file name. Two globals are
available: **`cm`** (the conversation) and **`player`**.

### `cm` — conversation

Each `send*`/`ask*` shows a dialog and **blocks** until the client answers, so scripts read as
straight-line code.

| Call | Shows | Returns |
|---|---|---|
| `cm.sendOk(text)` | text with an OK button | — |
| `cm.sendNext(text)` | text with a Next arrow | — |
| `cm.sendPrev(text)` | text with a Prev arrow | — |
| `cm.sendNextPrev(text)` | text with Prev + Next | — |
| `cm.askYesNo(text)` | a Yes/No prompt | `true` if Yes |
| `cm.askAccept(text)` | an Accept/Decline prompt | `true` if Accept |
| `cm.askMenu(text)` | a menu (use `#Ln#label#l` lines) | the selected index |
| `cm.sendSimple(text)` | alias for `askMenu` | the selected index |
| `cm.askText(text)` | a text-entry box | the entered string |
| `cm.dispose()` | ends the conversation | — |

Menu markup: `"#L0#First#l\r\n#L1#Second#l"` renders two clickable choices returning `0` / `1`.

### `player`

**Read:** `getName()` · `getLevel()` · `getMapId()` · `getMeso()` · `getExp()` · `getHp()` ·
`getMaxHp()` · `getGender()` · `getJob()` · `getStr()` · `getDex()` · `getInt()` · `getLuk()` ·
`getFame()` · `getAp()` · `getSp()`

**Change** (each persists and updates the client):
`gainMeso(n)` · `gainExp(n)` · `gainAp(n)` · `gainSp(n)` · `gainFame(n)` · `heal()` ·
`setJob(job)` · `gainMaxHp(n)` · `gainMaxMp(n)` · `warp(mapId)` · `warp(mapId, portal)`

**Items:** `gainItem(itemId, n)` (negative `n` takes items) · `haveItem(itemId)` ·
`itemQuantity(itemId)`

**Windows:** `openShop(shopId)` · `openStorage()`

**Quests:** `hasQuest(id)` · `isQuestDone(id)` · `startQuest(id)` · `completeQuest(id)`

### Quest scripts (`quest/{questId}.js`)

A quest whose wz data declares a script runs `CRONUS_SCRIPTS/quest/{questId}.js` instead of the
data-driven accept/complete: `function start()` handles the accept dialog and `function end()`
the completion, with the conversation bound as **`qm`** (same API as `cm`) plus the same
`player`. See `scripts/quest/1000.js` for a template.

### Example (a first-job instructor)

```javascript
function start() {
    if (player.getJob() != 0) { cm.sendOk("You've already chosen your path."); return; }
    if (player.getLevel() < 10) { cm.sendOk("Come back at level 10."); return; }
    if (cm.askYesNo("Become a Warrior?")) {
        player.setJob(100);
        player.gainSp(1);
        cm.sendOk("Rise, Warrior!");
    }
}
```

See `scripts/npc/9010000.js`, `9000021.js`, and `1012100.js` for working examples.

---

## Portal scripts — `CRONUS_SCRIPTS/portal/<scriptName>.js`

Run when the player steps on a portal whose wz `script` field equals the file name. A portal
script has **no dialog** — it runs once and typically just checks a condition and warps — so only
the **`player`** global is available (same API as above; `cm` is not provided).

### Example (a level-gated dungeon entrance)

```javascript
function start() {
    if (player.getLevel() >= 30) {
        player.warp(200000000); // dungeon map id
    }
    // else: nothing happens — the player doesn't pass.
}
```

See `scripts/portal/example.js`.

---

## Notes

- A script error (syntax or a bad call) is caught and ends the interaction rather than crashing the
  server — check the file if an NPC/portal does nothing.
- `setJob`, `gain*`, `warp`, and `heal` are **server-authoritative**: the change is applied and the
  client is told, so scripts can't be spoofed by the client.
- If `CRONUS_SCRIPTS` is unset, NPC dialogs and portal scripts are simply disabled.
