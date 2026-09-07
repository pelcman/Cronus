#!/usr/bin/env python3
"""
Remove the JMS v186 client's movement caps (speed 140%, jump 123%) so /gmmove's Speed/Jump
temporary stats take full effect (speed +200 -> 300%, jump +80 -> 180%).

The client clamps in six places, all the same two idioms (found by disassembling
JMS_v186.1_L.exe, which is not packed):

  SecondaryStat speed:  mov ecx,140 ; cmp eax,ecx ; jge +2 ; mov ecx,eax   -> store nSpeed
  SecondaryStat jump:   cmp eax,123 ; ... ; jl +3 ; push 123 ; pop eax     -> store nJump
  movement controller:  the same two shapes once more (speed cap is a parameter there).

Each patch flips one 2-byte conditional jump: `jge +2` -> `nop nop` (always take the
computed value) and `jl +3` -> `jmp +3` (always skip the 123 fallback). Nothing else moves.
The signature bytes around every site are checked before writing, so a different build is
refused rather than corrupted. A backup (`.orig`) is kept next to the exe.

Usage:
    python clientpatch_speedcap.py status  [exe]
    python clientpatch_speedcap.py apply   [exe]
    python clientpatch_speedcap.py revert  [exe]

Close the game client first (the exe is locked while it runs). The EmuClient loader's MSCRC
bypass is what lets a modified exe run, exactly as with Riremito's own edits to this file.
"""
import os, sys

DEFAULT_EXE = r"C:\Users\chro\Desktop\MS1PrivSvr\Client\MapleStory_v186_edit\JMS_v186.1_L.exe"

# (file offset of the signature, signature bytes, offset of the 2 bytes to patch within the
#  signature, original 2 bytes, patched 2 bytes, description)
SITES = [
    (0x33C2D3, bytes.fromhex("b98c00000083c4103bc17d028bc8"), 10, b"\x7d\x02", b"\x90\x90", "SecondaryStat A: speed = min(x, 140)"),
    (0x358817, bytes.fromhex("b98c00000083c4103bc17d028bc8"), 10, b"\x7d\x02", b"\x90\x90", "SecondaryStat B: speed = min(x, 140)"),
    (0x33C336, bytes.fromhex("83f87b59597c036a7b58"),          5, b"\x7c\x03", b"\xeb\x03", "SecondaryStat A: jump = min(x, 123)"),
    (0x48AD2C, bytes.fromhex("83f87b59597c036a7b58"),          5, b"\x7c\x03", b"\xeb\x03", "SecondaryStat B: jump = min(x, 123)"),
    (0x44B0C0, bytes.fromhex("8bc38b4d083bc17d028bc8"),        7, b"\x7d\x02", b"\x90\x90", "move controller: speed = min(x, cap)"),
    (0x44B0F2, bytes.fromhex("83f87b7c036a7b58"),              3, b"\x7c\x03", b"\xeb\x03", "move controller: jump = min(x, 123)"),
]


def inspect(data):
    """Returns (state, details): state is 'original', 'patched', 'mixed', or 'unknown'."""
    states = []
    for off, sig, at, orig, patched, name in SITES:
        cur = bytes(data[off:off + len(sig)])
        expected_orig = sig
        expected_patched = sig[:at] + patched + sig[at + 2:]
        if cur == expected_orig:
            states.append((name, "original"))
        elif cur == expected_patched:
            states.append((name, "patched"))
        else:
            states.append((name, f"UNKNOWN {cur.hex()}"))
    kinds = {s for _, s in states}
    if kinds == {"original"}:
        return "original", states
    if kinds == {"patched"}:
        return "patched", states
    if all(s in ("original", "patched") for _, s in states):
        return "mixed", states
    return "unknown", states


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("status", "apply", "revert"):
        print(__doc__)
        return 1

    cmd = sys.argv[1]
    exe = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_EXE
    backup = exe + ".orig"
    if not os.path.exists(exe):
        print(f"exe not found: {exe}")
        return 1

    data = bytearray(open(exe, "rb").read())
    state, states = inspect(data)
    for name, s in states:
        print(f"  {name}: {s}")
    print(f"state: {state}")

    if cmd == "status":
        return 0

    if state == "unknown":
        print("this is not the build these offsets were taken from -- refusing to touch it")
        return 2

    if cmd == "apply":
        if state == "patched":
            print("already patched")
            return 0
        if not os.path.exists(backup):
            orig = bytearray(data)
            for off, sig, at, o, _, _ in SITES:
                orig[off + at:off + at + 2] = o
            open(backup, "wb").write(orig)
            print(f"  wrote backup {os.path.basename(backup)}")
        for off, sig, at, o, p, name in SITES:
            data[off + at:off + at + 2] = p
        try:
            open(exe, "wb").write(data)
        except PermissionError:
            print("the exe is locked -- close the game client and run again")
            return 3
        print("DONE: caps removed. Start the client; /gmmove on now gives 300% speed and 180% jump.")
        return 0

    # revert
    if state == "original":
        print("already original")
        return 0
    for off, sig, at, o, p, name in SITES:
        data[off + at:off + at + 2] = o
    try:
        open(exe, "wb").write(data)
    except PermissionError:
        print("the exe is locked -- close the game client and run again")
        return 3
    print("DONE: original bytes restored.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
