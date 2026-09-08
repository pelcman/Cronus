#!/usr/bin/env python3
"""
Cosmic gap inventory: what Cosmic (GMS v83, Reference/Cosmic) scripts that Cronus does not — filtered
to what the JMS v186 client actually has, so the list is real work, not GMS-only ids.

    python DevTools\cosmic_gap.py            writes docs/COSMIC_GAP.md and prints a summary

Sources: Reference/Cosmic/scripts/{npc,quest,portal,reactor,event}, scripts/{npc,quest,portal,reactor},
gamedata.db (JMS client data: which NPCs have images and stand on which maps, which quests exist, which
portal script names and reactor ids the maps use). The order in each table is by how many JMS maps use
the thing, so the top rows are the most visible gaps. Regenerate after implementing a batch.
"""
import glob
import os
import re
import sqlite3
import sys
import zlib
from collections import defaultdict

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COSMIC = os.path.join(ROOT, "..", "Reference", "Cosmic", "scripts")
OURS = os.path.join(ROOT, "scripts")
DB = os.path.join(ROOT, "gamedata.db")
OUT = os.path.join(ROOT, "docs", "COSMIC_GAP.md")


def inflate(blob):
    return zlib.decompress(blob, -15).decode("utf-8", errors="replace")


def ids_in(folder):
    return {int(os.path.basename(f)[:-3]) for f in glob.glob(os.path.join(folder, "*.js")) if os.path.basename(f)[:-3].isdigit()}


def names_in(folder):
    return {os.path.basename(f)[:-3] for f in glob.glob(os.path.join(folder, "*.js"))}


def main():
    if not os.path.exists(DB) or not os.path.isdir(COSMIC):
        print("needs gamedata.db and Reference/Cosmic")
        return 2
    c = sqlite3.connect(DB)

    # --- JMS facts from the client data -----------------------------------------------------------
    npc_names = {}
    for m in re.finditer(r'<imgdir name="(\d+)">\s*<string name="name" value="([^"]*)"',
                         inflate(c.execute("select xml from wz_img where path='String/Npc.img.xml'").fetchone()[0])):
        npc_names[int(m.group(1))] = m.group(2)
    npc_imgs = {int(re.search(r"/(\d+)\.img\.xml$", p).group(1)) for (p,) in c.execute("select path from wz_img where path like 'Npc/%.img.xml'")}
    quest_ids = set(int(x) for x in re.findall(r'<imgdir name="(\d+)">',
                    inflate(c.execute("select xml from wz_img where path='Quest/Check.img.xml'").fetchone()[0])))
    map_names = {}
    for m in re.finditer(r'<imgdir name="(\d+)">(.*?)</imgdir>',
                         inflate(c.execute("select xml from wz_img where path='String/Map.img.xml'").fetchone()[0]), re.S):
        n = re.search(r'name="mapName" value="([^"]*)"', m.group(2))
        if n:
            map_names[int(m.group(1))] = n.group(1)

    npc_maps = defaultdict(set)        # npc id -> maps that place it
    portal_maps = defaultdict(set)     # portal script name -> maps
    reactor_maps = defaultdict(set)    # reactor id -> maps
    jms_maps = set()
    for (path, blob) in c.execute("select path, xml from wz_img where path like 'Map/Map%/%.img.xml'"):
        mid = int(re.search(r"/(\d+)\.img\.xml$", path).group(1))
        jms_maps.add(mid)
        xml = inflate(blob)
        for life in re.finditer(r'<imgdir name="\d+">\s*(?:<[^>]+>\s*)*?<string name="type" value="n"/>.*?</imgdir>', xml, re.S):
            idm = re.search(r'<string name="id" value="(\d+)"', life.group(0))
            if idm:
                npc_maps[int(idm.group(1))].add(mid)
        for pm in re.finditer(r'<string name="script" value="([^"]+)"', xml):
            portal_maps[pm.group(1)].add(mid)
        rsec = re.search(r'<imgdir name="reactor">(.*?)</imgdir>\s*(?:<imgdir name="(?!\d)|$)', xml, re.S)
        if rsec:
            for rm in re.finditer(r'<string name="id" value="(\d+)"', rsec.group(1)):
                reactor_maps[int(rm.group(1))].add(mid)

    # --- Cosmic vs ours ------------------------------------------------------------------------------
    cos_npc, our_npc = ids_in(os.path.join(COSMIC, "npc")), ids_in(os.path.join(OURS, "npc"))
    cos_quest, our_quest = ids_in(os.path.join(COSMIC, "quest")), ids_in(os.path.join(OURS, "quest"))
    cos_portal, our_portal = names_in(os.path.join(COSMIC, "portal")), names_in(os.path.join(OURS, "portal"))
    cos_reactor, our_reactor = ids_in(os.path.join(COSMIC, "reactor")), ids_in(os.path.join(OURS, "reactor"))
    cos_event = sorted(names_in(os.path.join(COSMIC, "event")))

    npc_gap = sorted((n for n in cos_npc - our_npc if n in npc_imgs), key=lambda n: (-len(npc_maps[n]), n))
    npc_gap_placed = [n for n in npc_gap if npc_maps[n]]
    quest_gap = sorted(q for q in cos_quest - our_quest if q in quest_ids)
    portal_gap = sorted((p for p in cos_portal - our_portal if p in portal_maps), key=lambda p: (-len(portal_maps[p]), p))
    reactor_gap = sorted((r for r in cos_reactor - our_reactor if r in reactor_maps), key=lambda r: (-len(reactor_maps[r]), r))
    jms_portal_unscripted = sorted((p for p in portal_maps if p not in our_portal and p not in cos_portal), key=lambda p: (-len(portal_maps[p]), p))

    event_hits = []
    for name in cos_event:
        text = open(os.path.join(COSMIC, "event", name + ".js"), encoding="utf-8", errors="replace").read()
        maps = sorted({int(x) for x in re.findall(r"\b(\d{9})\b", text) if int(x) in jms_maps})
        if maps:
            event_hits.append((name, maps))

    def mapname(mid):
        return map_names.get(mid, "")

    lines = []
    lines.append("# Cosmic → Cronus script gap (generated — do not edit by hand)\n")
    lines.append(f"Regenerate with `python DevTools/cosmic_gap.py`. Cosmic = GMS v83 content reference; every row is filtered to what the JMS v186 client has (image / quest / map / reactor), so each is real work. Ids and numbers must still be re-checked against v186 data when porting (`[DEV]` rule).\n")
    lines.append("| | Cosmic | Cronus | gap (JMS-relevant) |\n|---|---|---|---|")
    lines.append(f"| NPC scripts | {len(cos_npc)} | {len(our_npc)} | {len(npc_gap)} (on JMS maps: {len(npc_gap_placed)}) |")
    lines.append(f"| quest scripts | {len(cos_quest)} | {len(our_quest)} | {len(quest_gap)} |")
    lines.append(f"| portal scripts | {len(cos_portal)} | {len(our_portal)} | {len(portal_gap)} (+{len(jms_portal_unscripted)} JMS portal scripts neither has) |")
    lines.append(f"| reactor scripts | {len(cos_reactor)} | {len(our_reactor)} | {len(reactor_gap)} |")
    lines.append(f"| event scripts (PQ / boss / ride) | {len(cos_event)} | — | {len(event_hits)} reference JMS maps |\n")

    lines.append("## NPCs Cosmic scripts and Cronus does not (with a JMS image), most-placed first\n")
    lines.append("| npc | JMS name | JMS maps | Cosmic file |\n|---|---|---|---|")
    for n in npc_gap:
        maps = sorted(npc_maps[n])
        shown = ", ".join(f"{m} {mapname(m)}".strip() for m in maps[:3]) + (" …" if len(maps) > 3 else "")
        lines.append(f"| {n} | {npc_names.get(n, '')} | {len(maps)}: {shown} | `Reference/Cosmic/scripts/npc/{n}.js` |")

    lines.append("\n## Quest scripts Cosmic has for quests that exist in JMS\n")
    lines.append("| quest | Cosmic file |\n|---|---|")
    for q in quest_gap:
        lines.append(f"| {q} | `Reference/Cosmic/scripts/quest/{q}.js` |")

    lines.append("\n## Portal scripts JMS maps use, that Cosmic has and Cronus lacks\n")
    lines.append("| script | JMS maps | Cosmic file |\n|---|---|---|")
    for p in portal_gap:
        maps = sorted(portal_maps[p])
        shown = ", ".join(f"{m} {mapname(m)}".strip() for m in maps[:3]) + (" …" if len(maps) > 3 else "")
        lines.append(f"| {p} | {len(maps)}: {shown} | `Reference/Cosmic/scripts/portal/{p}.js` |")

    lines.append("\n## Portal scripts JMS maps use that neither Cosmic nor Cronus has (JMS-only content)\n")
    lines.append("| script | JMS maps |\n|---|---|")
    for p in jms_portal_unscripted:
        maps = sorted(portal_maps[p])
        shown = ", ".join(f"{m} {mapname(m)}".strip() for m in maps[:3]) + (" …" if len(maps) > 3 else "")
        lines.append(f"| {p} | {len(maps)}: {shown} |")

    lines.append("\n## Reactor scripts for reactors JMS maps place, that Cosmic has and Cronus lacks\n")
    lines.append("| reactor | JMS maps | Cosmic file |\n|---|---|---|")
    for r in reactor_gap:
        maps = sorted(reactor_maps[r])
        shown = ", ".join(f"{m} {mapname(m)}".strip() for m in maps[:3]) + (" …" if len(maps) > 3 else "")
        lines.append(f"| {r} | {len(maps)}: {shown} | `Reference/Cosmic/scripts/reactor/{r}.js` |")

    lines.append("\n## Cosmic event scripts (PQ / boss / ride instances) that name JMS maps\n")
    lines.append("| event | JMS maps referenced |\n|---|---|")
    for name, maps in event_hits:
        shown = ", ".join(f"{m} {mapname(m)}".strip() for m in maps[:4]) + (" …" if len(maps) > 4 else "")
        lines.append(f"| {name} | {len(maps)}: {shown} |")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    print(f"wrote {os.path.relpath(OUT, ROOT)}")
    print(f"NPC gap {len(npc_gap)} (placed on JMS maps {len(npc_gap_placed)}), quest gap {len(quest_gap)}, portal gap {len(portal_gap)} (+{len(jms_portal_unscripted)} JMS-only unscripted), reactor gap {len(reactor_gap)}, events touching JMS maps {len(event_hits)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
