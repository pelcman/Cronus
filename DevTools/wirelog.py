# -*- coding: utf-8 -*-
"""Wire-log forensics: annotate a CRONUS_DEBUG log and pull crash contexts.

Usage:
    python DevTools/wirelog.py                    # latest log, every disconnect context
    python DevTools/wirelog.py <logfile>          # a specific log
    python DevTools/wirelog.py --tail 30          # packets of context per disconnect
    python DevTools/wirelog.py --summary          # opcode histogram + session overview
    python DevTools/wirelog.py --opcode 0069      # every line of one opcode, annotated

Every packet line is annotated with its opcode NAME (from data/opcodes/*.properties) and,
for the common suspects, a decoded gist: quest requests (action/quest id), NPC selections
(object id), portal names, chat text, transfers. This automates the manual analysis used
to crack the card-pickup, quest-completion, and parcel-layout investigations.
"""
import io
import os
import re
import sys
import glob

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_opcodes(path):
    table = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\s*(\w+)\s*=\s*@([0-9A-Fa-f]+)", line)
        if m:
            table[int(m.group(2), 16)] = m.group(1)
    return table


CP = load_opcodes(os.path.join(ROOT, "data", "opcodes", "JMS_v186_ClientPacket.properties"))
LP = load_opcodes(os.path.join(ROOT, "data", "opcodes", "JMS_v186_ServerPacket.properties"))

RECV = re.compile(r"^\[(\d\d:\d\d:\d\d)\] \[(\w+)\] recv opcode 0x([0-9A-F]{4}) \((\d+) bytes\): ([0-9A-F]+)")
SEND = re.compile(r"^\[send:Server\] opcode 0x([0-9A-F]{4}) \((\d+) bytes\): ([0-9A-F]+)")
DISC = re.compile(r"^\[(\d\d:\d\d:\d\d)\] \[(\w+)\] (disconnected|connected)")


def sjis(hexstr):
    try:
        return bytes.fromhex(hexstr).decode("cp932", errors="replace")
    except ValueError:
        return "?"


def le(hexstr, offset, size):
    b = hexstr[offset * 2:(offset + size) * 2]
    if len(b) < size * 2:
        return None
    return int.from_bytes(bytes.fromhex(b), "little")


def gist_recv(op, body):
    """A one-phrase decode of the interesting client packets (body excludes the opcode)."""
    name = CP.get(op, "?")
    try:
        if name == "CP_UserQuestRequest":
            action = le(body, 0, 1)
            quest = le(body, 1, 2)
            return f"action={action} quest={quest}"
        if name == "CP_UserSelectNpc":
            return f"npcObj={le(body, 0, 4)}"
        if name == "CP_UserChat":
            ln = le(body, 4, 2) or 0
            return f"'{sjis(body[12:12 + ln * 2])}'"
        if name == "CP_UserTransferFieldRequest":
            mapid = le(body, 1, 4)
            ln = le(body, 5, 2) or 0
            portal = sjis(body[14:14 + ln * 2])
            return f"map={mapid} portal='{portal}'"
        if name == "CP_UserPortalScriptRequest":
            ln = le(body, 1, 2) or 0
            return f"portal='{sjis(body[6:6 + ln * 2])}'"
        if name == "CP_UserScriptMessageAnswer":
            return f"type={le(body, 0, 1)} action={le(body, 1, 1)}"
        if name == "CP_UserParcelRequest":
            return f"action={le(body, 0, 1)}"
        if name == "CP_UserShopRequest":
            return f"op={le(body, 0, 1)}"
        if name == "CP_UserHit":
            return f"attackIdx={le(body, 4, 1)} damage={le(body, 6, 4)}"
    except Exception:
        pass
    return ""


def gist_send(op, body):
    name = LP.get(op, "?")
    try:
        if name == "LP_UserQuestResult":
            return f"result={le(body, 0, 1)} quest={le(body, 1, 2)}"
        if name == "LP_Message" and le(body, 0, 1) == 1:
            return f"questRecord quest={le(body, 1, 2)} state={le(body, 3, 1)}"
        if name == "LP_UserEffectLocal":
            return f"effect={le(body, 0, 1)}"
        if name == "LP_ScriptMessage":
            return f"npc={le(body, 1, 4)} type={le(body, 5, 1)}"
        if name == "LP_Parcel":
            return f"action=0x{le(body, 0, 1):02X}"
        if name == "LP_TransferFieldReqIgnored":
            return f"reason={le(body, 0, 1)}"
        if name == "LP_ShopResult":
            return f"code={le(body, 0, 1)}"
    except Exception:
        pass
    return ""


def annotate(line):
    m = RECV.match(line)
    if m:
        ts, ch, op_hex, _, hexs = m.groups()
        op = int(op_hex, 16)
        body = hexs[4:]  # strip the 2-byte opcode echo
        extra = gist_recv(op, body)
        return f"{ts} {ch} --> {CP.get(op, '0x' + op_hex):36s} {extra}"

    m = SEND.match(line)
    if m:
        op_hex, _, hexs = m.groups()
        op = int(op_hex, 16)
        body = hexs[4:]
        extra = gist_send(op, body)
        return f"         <-- {LP.get(op, '0x' + op_hex):36s} {extra}"

    m = DISC.match(line)
    if m:
        return f"{m.group(1)} {m.group(2)} ### {m.group(3).upper()} ###"

    return None


def main():
    args = sys.argv[1:]
    tail = 25
    summary = False
    opcode_filter = None
    logfile = None
    i = 0
    while i < len(args):
        if args[i] == "--tail" and i + 1 < len(args):
            tail = int(args[i + 1]); i += 2
        elif args[i] == "--summary":
            summary = True; i += 1
        elif args[i] == "--opcode" and i + 1 < len(args):
            opcode_filter = int(args[i + 1], 16); i += 2
        else:
            logfile = args[i]; i += 1

    if logfile is None:
        logs = glob.glob(os.path.join(ROOT, "src", "Cronus.Server.Host", "bin", "*", "net*", "logs", "*.log"))
        if not logs:
            print("no logs found"); return 1
        logfile = max(logs, key=os.path.getmtime)

    print(f"# {logfile}")
    lines = open(logfile, encoding="utf-8", errors="replace").read().split("\n")

    if opcode_filter is not None:
        for line in lines:
            m = RECV.match(line) or SEND.match(line)
            if m and int(m.group(3 if len(m.groups()) >= 5 else 1), 16) == opcode_filter:
                print(annotate(line))
        return 0

    if summary:
        from collections import Counter
        counts = Counter()
        for line in lines:
            m = RECV.match(line)
            if m:
                counts["--> " + CP.get(int(m.group(3), 16), m.group(3))] += 1
                continue
            m = SEND.match(line)
            if m:
                counts["<-- " + LP.get(int(m.group(1), 16), m.group(1))] += 1
        for name, n in counts.most_common(40):
            print(f"{n:7d}  {name}")
        return 0

    # Default: context before every disconnect.
    packet_idx = [i for i, l in enumerate(lines) if RECV.match(l) or SEND.match(l) or DISC.match(l)]
    shown = 0
    for i, l in enumerate(lines):
        if "disconnected" not in l:
            continue
        shown += 1
        print(f"\n===== disconnect #{shown}: {l.strip()[:60]}")
        prior = [j for j in packet_idx if j < i][-tail:]
        for j in prior:
            a = annotate(lines[j])
            if a:
                print("  " + a)
    if shown == 0:
        print("(no disconnects in this log)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
