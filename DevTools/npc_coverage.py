# -*- coding: utf-8 -*-
"""NPC coverage inventory: docs/NPC_COVERAGE.md を生成する。

全マップ(gamedata.db)の life から実際にスポーンする NPC を集め、
  - scripts/npc/{id}.js の有無
  - ショップ表 (init_data_set.sql の shops.npcid)
  - クエスト関与 (Quest/Check.img の npc 値 → クライアントのクエストUIで動く)
  - Npc.wz info のヒント (script 名 / trunk / parcel / shop フラグ = 本来の機能)
を突き合わせて、対応状況を1行1NPCで表にする。再生成:
    python DevTools/npc_coverage.py
"""
import io
import os
import re
import sqlite3
import sys
import zlib
from collections import defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DB = os.path.join(ROOT, "gamedata.db")
OUT = os.path.join(ROOT, "docs", "NPC_COVERAGE.md")

db = sqlite3.connect(DB)


def read(path):
    row = db.execute("SELECT xml FROM wz_img WHERE path=?", (path,)).fetchone()
    return zlib.decompress(row[0], -15).decode("utf-8") if row else None


# ---- names -------------------------------------------------------------------------------
names = {}
for m in re.finditer(r'<imgdir name="(\d+)"><string name="name" value="([^"]*)"', read("String/Npc.img.xml") or ""):
    names[int(m.group(1))] = m.group(2)

# ---- spawns: map id -> npc ids (all maps) ------------------------------------------------
spawns = defaultdict(set)   # npc -> set(map)
map_names = {}
smap = read("String/Map.img.xml") or ""
for m in re.finditer(r'<imgdir name="(\d+)"><string name="streetName" value="([^"]*)"/><string name="mapName" value="([^"]*)"', smap):
    map_names[int(m.group(1))] = m.group(3)
for (path, blob) in db.execute("SELECT path, xml FROM wz_img WHERE path GLOB 'Map/Map*/*.img.xml'"):
    try:
        mid = int(path.split("/")[-1].split(".")[0])
    except ValueError:
        continue
    xml = zlib.decompress(blob, -15).decode("utf-8")
    for mm in re.finditer(r'<string name="type" value="n"/><string name="id" value="(\d+)"', xml):
        spawns[int(mm.group(1))].add(mid)

# ---- shops (SQL dump) --------------------------------------------------------------------
shop_npcs = set()
sql_path = os.environ.get("CRONUS_SHOPS", os.path.join(ROOT, "..", "Reference", "JMSv186", "sql", "init_data_set.sql"))
if os.path.exists(sql_path):
    sql = open(sql_path, encoding="utf-8", errors="replace").read()
    for m in re.finditer(r"INSERT INTO `?shops`?[^;]*?VALUES\s*(.+?);", sql, re.S):
        for row in re.finditer(r"\((\d+),\s*(\d+)", m.group(1)):
            shop_npcs.add(int(row.group(2)))

# ---- quests ------------------------------------------------------------------------------
quest_npcs = defaultdict(list)
check = read("Quest/Check.img.xml") or ""
for b in re.split(r'(?=<imgdir name="\d+"><imgdir name="[01]">)', check):
    qm = re.match(r'<imgdir name="(\d+)">', b)
    if not qm:
        continue
    for nm in re.finditer(r'<int name="npc" value="(\d+)"/>', b):
        quest_npcs[int(nm.group(1))].append(int(qm.group(1)))

# ---- wz hints ----------------------------------------------------------------------------
def wz_hint(nid):
    xml = read(f"Npc/{nid:07d}.img.xml")
    if not xml:
        return ""
    info = re.search(r'<imgdir name="info">(.*?)<imgdir name="(?:stand|move|say|blink)"', xml, re.S)
    seg = info.group(1) if info else xml[:1500]
    bits = []
    m = re.search(r'<string name="script" value="([^"]*)"', seg)
    if m:
        bits.append("script:" + m.group(1))
    for flag in ("trunk", "parcel", "shop", "storebank", "guildrank", "guild"):
        if re.search(rf'<int name="{flag}" value="1"/>', seg):
            bits.append(flag)
    return " ".join(bits)


# ---- our scripts -------------------------------------------------------------------------
have_script = set()
for f in os.listdir(os.path.join(ROOT, "scripts", "npc")):
    if f.endswith(".js"):
        try:
            have_script.add(int(f[:-3]))
        except ValueError:
            pass

# ---- report ------------------------------------------------------------------------------
spawning = sorted(spawns)
rows = []
counts = defaultdict(int)
for nid in spawning:
    quests = quest_npcs.get(nid, [])
    status = ("script" if nid in have_script else
              "shop" if nid in shop_npcs else
              "quest-data" if quests else
              "none")
    counts[status] += 1
    hint = wz_hint(nid)
    maps = sorted(spawns[nid])
    where = f"{maps[0]} {map_names.get(maps[0], '')}" + (f" 他{len(maps)-1}" if len(maps) > 1 else "")
    rows.append((nid, names.get(nid, "?"), status, len(quests), hint, where))

with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("# NPC coverage (generated — do not edit by hand)\n\n")
    f.write("Regenerate with `python DevTools/npc_coverage.py` (needs gamedata.db).\n\n")
    f.write("Status meaning: **script** = server script exists / **shop** = vendor via the shop\n")
    f.write("table / **quest-data** = the client's own quest UI drives it (data-driven accept &\n")
    f.write("complete work server-side) / **none** = nothing behind it yet (the click falls back\n")
    f.write("to the generic line). The wz-hint column shows what the ORIGINAL server had for it —\n")
    f.write("`script:<name>` names Nexon's server script (an authoring lead), trunk = storage,\n")
    f.write("parcel = home delivery.\n\n")
    total = len(spawning)
    f.write(f"## Summary\n\n")
    f.write(f"- NPCs spawning across all maps: **{total}**\n")
    for k in ("script", "shop", "quest-data", "none"):
        f.write(f"- {k}: **{counts[k]}** ({counts[k]*100//max(1,total)}%)\n")
    f.write("\n## Actionable queue (spawning, status=none, with a wz script hint)\n\n")
    f.write("| NPC | 名前 | wzヒント | 代表マップ |\n|---|---|---|---|\n")
    for nid, name, status, nq, hint, where in rows:
        if status == "none" and hint.startswith("script:"):
            f.write(f"| {nid} | {name} | {hint} | {where} |\n")
    f.write("\n## Full list (spawning NPCs)\n\n")
    f.write("| NPC | 名前 | 状態 | クエスト数 | wzヒント | 代表マップ |\n|---|---|---|---|---|---|\n")
    for nid, name, status, nq, hint, where in rows:
        f.write(f"| {nid} | {name} | {status} | {nq or ''} | {hint} | {where} |\n")

print(f"wrote {OUT}: {len(spawning)} spawning NPCs, {dict(counts)}")
