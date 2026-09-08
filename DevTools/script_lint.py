#!/usr/bin/env python3
"""
Script id lint: every item / map / mob / npc / skill id an NPC, quest, portal or reactor script shows
to the client or hands to the server must exist in the client's own data, or the client crashes
(a "#t<item>#" of an unknown item is a reliable crash) or the server sends nonsense.

    python DevTools\script_lint.py [gamedata.db] [scripts dir]

Reads the String.wz name tables and the Map.wz image list from gamedata.db (the client's data), then
scans scripts/**/*.js for:
  dialog tags   #t<item>#  #i<item>#  #z<item>#  #v<item>#  #c<item>#   #m<map>#   #o<mob>#   #p<npc>#   #q<skill>#
  API ids       gainItem(<item>  haveItem(<item>  itemQuantity(<item>  warp(<map>  warpPortal(<map>
                spawnMob(<mob>  openShop(...)  (shops are server data, not checked here)
Simple constants ("var X = 4031242;") are substituted, so "#t" + X + "#" is checked too.
Exit code 1 when anything is missing. Also prints the fieldType histogram of all maps (the special
client field classes that need their own packets on entry — docs/TASK.md フェーズ0).
"""
import glob
import os
import re
import sqlite3
import sys
import xml.etree.ElementTree as ET
import zlib
from collections import Counter, defaultdict

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DB = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "gamedata.db")
SCRIPTS = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "scripts")


def numeric_dirs(xml_text, min_depth=1):
    """All numeric <imgdir name> values at depth >= min_depth under the image root."""
    ids = set()
    root = ET.fromstring(xml_text)

    def walk(node, depth):
        for child in node:
            if child.tag == "imgdir":
                name = child.get("name", "")
                if depth >= min_depth and name.isdigit():
                    ids.add(int(name))
                walk(child, depth + 1)

    walk(root, 1)
    return ids


def inflate(blob):
    """gamedata.db stores each image's wz_xml as raw Deflate (see Cronus.Data WzIngest/WzStore)."""
    return zlib.decompress(blob, -15).decode("utf-8", errors="replace") if isinstance(blob, (bytes, bytearray)) else blob


def load_tables(db_path):
    c = sqlite3.connect(db_path)
    rows = {path: inflate(xml) for path, xml in c.execute("select path, xml from wz_img where path like 'String/%'")}
    items, maps, mobs, npcs, skills = set(), set(), set(), set(), set()
    for name in ("Cash", "Consume", "Eqp", "Etc", "Ins", "Pet"):
        xml = rows.get(f"String/{name}.img.xml")
        if xml:
            items |= numeric_dirs(xml)
    maps |= numeric_dirs(rows["String/Map.img.xml"], min_depth=2) if "String/Map.img.xml" in rows else set()
    mobs |= numeric_dirs(rows["String/Mob.img.xml"]) if "String/Mob.img.xml" in rows else set()
    npcs |= numeric_dirs(rows["String/Npc.img.xml"]) if "String/Npc.img.xml" in rows else set()
    skills |= numeric_dirs(rows["String/Skill.img.xml"]) if "String/Skill.img.xml" in rows else set()
    map_imgs = set()
    field_types = Counter()
    for (path, blob) in c.execute("select path, xml from wz_img where path like 'Map/Map%/%.img.xml'"):
        m = re.search(r"/(\d+)\.img\.xml$", path)
        if not m:
            continue
        map_imgs.add(int(m.group(1)))
        xml = inflate(blob)
        ft = re.search(r'<int name="fieldType" value="(-?\d+)"', xml)
        field_types[int(ft.group(1)) if ft else 0] += 1
    return items, maps, mobs, npcs, skills, map_imgs, field_types


TAG = re.compile(r"#([tizvcmopq])(\d+)#")
CONST = re.compile(r"\bvar\s+([A-Za-z_]\w*)\s*=\s*(\d{4,})\s*;")
TAG_CONST = re.compile(r'#([tizvcmopq])"\s*\+\s*([A-Za-z_]\w*)\s*\+\s*"#')
API = re.compile(r"\b(gainItem|haveItem|itemQuantity|warp|warpPortal|spawnMob|rememberMap)\(\s*(\d+)")
API_CONST = re.compile(r"\b(gainItem|haveItem|itemQuantity|warp|warpPortal|spawnMob)\(\s*([A-Za-z_]\w*)\s*[,)]")

KIND = {"t": "item", "i": "item", "z": "item", "v": "item", "c": "item", "m": "map", "o": "mob", "p": "npc", "q": "skill",
        "gainItem": "item", "haveItem": "item", "itemQuantity": "item", "warp": "map", "warpPortal": "map", "rememberMap": "map", "spawnMob": "mob"}


def main():
    if not os.path.exists(DB):
        print(f"gamedata.db not found: {DB}")
        return 2
    items, maps, mobs, npcs, skills, map_imgs, field_types = load_tables(DB)
    print(f"tables: {len(items)} items, {len(maps)} named maps, {len(map_imgs)} map images, {len(mobs)} mobs, {len(npcs)} npcs, {len(skills)} skills")
    known = {"item": items, "map": maps | map_imgs, "mob": mobs, "npc": npcs, "skill": skills}

    missing = defaultdict(list)   # (kind, id) -> [file:line]
    checked = 0
    for path in sorted(glob.glob(os.path.join(SCRIPTS, "**", "*.js"), recursive=True)):
        text = open(path, encoding="utf-8", errors="replace").read()
        consts = {m.group(1): int(m.group(2)) for m in CONST.finditer(text)}
        rel = os.path.relpath(path, ROOT)
        for lineno, line in enumerate(text.splitlines(), 1):
            refs = [(KIND[m.group(1)], int(m.group(2))) for m in TAG.finditer(line)]
            refs += [(KIND[m.group(1)], consts[m.group(2)]) for m in TAG_CONST.finditer(line) if m.group(2) in consts]
            refs += [(KIND[m.group(1)], int(m.group(2))) for m in API.finditer(line)]
            refs += [(KIND[m.group(1)], consts[m.group(2)]) for m in API_CONST.finditer(line) if m.group(2) in consts]
            for kind, value in refs:
                checked += 1
                if kind == "map" and value == 999999999:
                    continue               # the wz "no target" sentinel
                if value not in known[kind]:
                    missing[(kind, value)].append(f"{rel}:{lineno}")

    print(f"checked {checked} references in scripts")
    if missing:
        print(f"\n{len(missing)} unknown id(s) — each is a client crash or a silent server no-op:")
        for (kind, value), where in sorted(missing.items()):
            print(f"  {kind:5s} {value:<10d} {', '.join(where[:4])}{' …' if len(where) > 4 else ''}")
    else:
        print("no unknown ids.")

    print("\nfieldType histogram (Map.wz):")
    for ft, n in sorted(field_types.items()):
        print(f"  fieldType {ft:3d}: {n} maps")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
