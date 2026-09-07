#!/usr/bin/env python3
"""
Remove the JMS v186 client's movement and attack-speed caps so /gmmove's multipliers take full
effect: speed 140% -> unlimited, jump 123% -> unlimited, attack speed degree "2 = fastest" ->
unlimited (down to 1/8 of the normal frame time), walking-animation pace 140% -> 1000%.

All sites were found by disassembling JMS_v186.1_L.exe (not packed) — the same two idioms:

  speed clamp        mov ecx,140 ; cmp eax,ecx ; jge +2 ; mov ecx,eax          -> nSpeed
  jump clamp         cmp eax,123 ; ... ; jl +3 ; push 123 ; pop eax            -> nJump
  move controller    the same two shapes once more (speed cap is a parameter)
  attack speed       cmp eax,2 ; jg +3 ; push 2 ; pop eax ; (min 10) ;
                     frame delay = delay * (degree + 10) / 16                  (4 action builders)
  walk animation     cmp ecx,70 ; jg +3 ; push 70 ; pop ecx ; mov eax,140     (pace = 100/speed%)
  local physics      CUserLocal: speed = min(total, cap) with cap = 140 (default immediate) or
                     190 when riding; jump = min(max(total, 80), 123); then *0.01 into the
                     vector controller (+0x84 speed, +0x48 jump). Three copies (on foot / two
                     riding paths) -- the on-foot copy is what actually moves the GM character.
  SecondaryStat C    a third min(x,140) pair (speed and jump) feeding the stat window's values.
  CUser -> vec ctrl  min(speed,140) before the remote-user vector controller (other players).

Each patch flips a 2-byte conditional jump (`jge +2` -> `nop nop`, `jl +2/+3` -> `jmp`, `jg +3` -> `jmp +3`)
or widens one immediate (140 -> 1000 for animation pace, 140/190 -> 10000 for the physics caps). Instruction lengths never change. The signature bytes around
every site are checked before writing, so a different build is refused rather than corrupted. A
backup (`.orig`) is kept next to the exe.

Usage:
    python clientpatch_speedcap.py status  [exe]
    python clientpatch_speedcap.py apply   [exe]
    python clientpatch_speedcap.py revert  [exe]

Close the game client first (the exe is locked while it runs). The EmuClient loader's MSCRC
bypass is what lets a modified exe run, exactly as with Riremito's own edits to this file.
"""
import os, sys

DEFAULT_EXE = r"C:\Users\chro\Desktop\MS1PrivSvr\Client\MapleStory_v186_edit\JMS_v186.1_L.exe"

# (file offset of the signature, signature bytes as in the original build, offset of the patch
#  within the signature, patched bytes, description)
SITES = [
    (0x33C2D3, bytes.fromhex("b98c00000083c4103bc17d028bc8"), 10, bytes.fromhex("9090"), "SecondaryStat A: speed = min(x, 140)"),
    (0x358817, bytes.fromhex("b98c00000083c4103bc17d028bc8"), 10, bytes.fromhex("9090"), "SecondaryStat B: speed = min(x, 140)"),
    (0x33C336, bytes.fromhex("83f87b59597c036a7b58"),          5, bytes.fromhex("eb03"), "SecondaryStat A: jump = min(x, 123)"),
    (0x48AD2C, bytes.fromhex("83f87b59597c036a7b58"),          5, bytes.fromhex("eb03"), "SecondaryStat B: jump = min(x, 123)"),
    (0x44B0C0, bytes.fromhex("8bc38b4d083bc17d028bc8"),        7, bytes.fromhex("9090"), "move controller: speed = min(x, cap)"),
    (0x44B0F2, bytes.fromhex("83f87b7c036a7b58"),              3, bytes.fromhex("eb03"), "move controller: jump = min(x, 123)"),
    (0x061FAE, bytes.fromhex("83f8027f036a02586a0a59"),        3, bytes.fromhex("eb03"), "action frames A: attack speed = max(d, 2)"),
    (0x06264E, bytes.fromhex("83f8027f036a025883f80a"),        3, bytes.fromhex("eb03"), "action frames B: attack speed = max(d, 2)"),
    (0x06534E, bytes.fromhex("83f8027f036a025883f80a"),        3, bytes.fromhex("eb03"), "action frames C: attack speed = max(d, 2)"),
    (0x065BBD, bytes.fromhex("83f8027f036a025883f80a"),        3, bytes.fromhex("eb03"), "action frames D: attack speed = max(d, 2)"),
    (0x0625CA, bytes.fromhex("83f9467f036a4659b88c000000"),    9, bytes.fromhex("e8030000"), "walk animation A: pace cap 140% -> 1000%"),
    (0x065B6D, bytes.fromhex("83f9467f036a4659b88c000000"),    9, bytes.fromhex("e8030000"), "walk animation B: pace cap 140% -> 1000%"),
    # -- second pass (2026-09-07): the clamps the stat window and the local player's physics really use
    (0x32F977, bytes.fromhex("c4103bc68bc87c028bce8b45"), 6, bytes.fromhex("eb02"), "SecondaryStat C (local): speed = min(x, 140)"),
    (0x32F9F4, bytes.fromhex("c4103bc68bc87c028bce8b45"), 6, bytes.fromhex("eb02"), "SecondaryStat C (local): jump = min(x, 140)"),
    (0x34C4A9, bytes.fromhex("003bc18945f07c03894df0db"), 6, bytes.fromhex("eb03"), "CUser -> vector controller: speed = min(x, 140)"),
    (0x687A9F, bytes.fromhex("ff5959eb05b88c0000008945e08d"), 6, bytes.fromhex("10270000"), "CUserLocal physics: default speed cap 140 -> 10000"),
    (0x687B8B, bytes.fromhex("e03bca894df07c038955f08b"), 6, bytes.fromhex("eb03"), "CUserLocal physics (riding): speed = min(x, cap)"),
    (0x687BA3, bytes.fromhex("593bc18945ec7c03894decdb"), 6, bytes.fromhex("eb03"), "CUserLocal physics (riding): jump = min(x, 123)"),
    (0x687E9E, bytes.fromhex("6a465803d8b8be0000003bd8895d"), 6, bytes.fromhex("10270000"), "CUserLocal physics (riding+): speed cap 190 -> 10000"),
    (0x687EC4, bytes.fromhex("593bc18945ec7c03894decdb"), 6, bytes.fromhex("eb03"), "CUserLocal physics (riding+): jump = min(x, 123)"),
    (0x688003, bytes.fromhex("593bc18945ec7c03894decdb"), 6, bytes.fromhex("eb03"), "CUserLocal physics (on foot): jump = min(x, 123)"),
]


def patched_signature(sig, at, patched):
    return sig[:at] + patched + sig[at + len(patched):]


def inspect(data):
    """Returns (state, details): state is 'original', 'patched', 'mixed', or 'unknown'."""
    states = []
    for off, sig, at, patched, name in SITES:
        cur = bytes(data[off:off + len(sig)])
        if cur == sig:
            states.append((name, "original"))
        elif cur == patched_signature(sig, at, patched):
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
            for off, sig, at, patched, _ in SITES:
                orig[off:off + len(sig)] = sig
            open(backup, "wb").write(orig)
            print(f"  wrote backup {os.path.basename(backup)}")
        for off, sig, at, patched, name in SITES:
            data[off + at:off + at + len(patched)] = patched
        try:
            open(exe, "wb").write(data)
        except PermissionError:
            print("the exe is locked -- close the game client and run again")
            return 3
        print("DONE: caps removed (speed, jump, attack speed, walk animation). Start the client.")
        return 0

    # revert
    if state == "original":
        print("already original")
        return 0
    for off, sig, at, patched, name in SITES:
        data[off:off + len(sig)] = sig
    try:
        open(exe, "wb").write(data)
    except PermissionError:
        print("the exe is locked -- close the game client and run again")
        return 3
    print("DONE: original bytes restored.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
