#!/usr/bin/env python3
"""
Crash harness: runs the /sweep crash inventory with a REAL client and no human at the keyboard.

    python DevTools\crash_harness.py probe                 launch a client, screenshot it every few
                                                           seconds (calibration; no input is sent)
    python DevTools\crash_harness.py login                 launch + log in + enter the game, then stop
    python DevTools\crash_harness.py run  [from] [to] [sec] launch, log in, /sweep maps ..., babysit
    python DevTools\crash_harness.py resume [sec]          same, but /sweep resume

What "babysit" means: the harness watches the client process and the server's sweep-progress.txt.
When the client dies, hangs, or the progress file stops advancing (the client was disconnected),
it records the last logged map in sweep-crashes.txt, kills what is left of the client, launches a
fresh one, logs in again and sends /sweep resume — until the progress file ends with "# done".
The server side helps: a client lost mid-sweep is saved at the rescue town (so the next login does
not replay the crash) and "# crash? <map>" is written to the progress file.

The client is driven by keyboard/mouse (pyautogui) on the foreground window, so the harness owns
the mouse and keyboard while it runs — start it and leave the machine alone (overnight is ideal).
It only ever targets the window of the process it launched itself.

Requirements: Windows, Python 3, pyautogui, Pillow (pip install pyautogui pillow). The server
must be running with the launched client's folder as its game data (Client\MapleStory_v186).
"""
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time
from datetime import datetime

import pyautogui

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

pyautogui.FAILSAFE = False   # the mouse parked in a corner must not abort an overnight run
pyautogui.PAUSE = 0.05

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIENT_EXE = os.environ.get("CRONUS_CLIENT_PATH") or os.path.join(ROOT, "..", "Client", "MapleStory_v186", "JMS_v186.1_L.exe")
HOST_BIN = os.path.join(ROOT, "src", "Cronus.Server.Host", "bin", "Debug", "net10.0")
PROGRESS = os.path.join(HOST_BIN, "sweep-progress.txt")
CRASHES = os.path.join(HOST_BIN, "sweep-crashes.txt")
SHOTS = os.path.join(ROOT, "DevTools", "harness-shots")

ACCOUNT = os.environ.get("CRONUS_HARNESS_ACCOUNT", "cronusbot1")   # the debug bot's account: password "bot",
PASSWORD = os.environ.get("CRONUS_HARNESS_PASSWORD", "bot")        # character CronusBot1 (level 30) already exists

# --- screen positions inside the 800x600 client area (calibrated with `probe`) -------------------
# Login screen: the ID box, the password box, the login button.
LOGIN_ID_XY = (370, 290)
LOGIN_PW_XY = (370, 316)
LOGIN_BTN_XY = (478, 302)
# World select: the world button, then a channel button.
WORLD_XY = (215, 300)
CHANNEL_XY = (475, 195)
# Character select: the first character slot, then the "select" button.
CHAR_XY = (150, 300)
CHAR_SELECT_BTN_XY = (720, 545)

LOGIN_WAIT = 25          # seconds from launch until the login screen is usable
STAGE_WAIT = 6           # seconds between UI stages
ENTER_WAIT = 20          # seconds for the field to load after character select
STALL_FACTOR = 6         # progress not advancing for dwell*STALL_FACTOR (+30 s) = the client is gone

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32
EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)


def log(msg):
    line = f"[{datetime.now():%H:%M:%S}] {msg}"
    print(line, flush=True)


# ---------------------------------------------------------------------------------------------
# window plumbing
# ---------------------------------------------------------------------------------------------

def windows_of_pid(pid):
    found = []

    def cb(hwnd, _):
        owner = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(hwnd):
            n = user32.GetWindowTextLengthW(hwnd)
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(hwnd, buf, n + 1)
            r = wt.RECT()
            user32.GetClientRect(hwnd, ctypes.byref(r))
            if r.right - r.left >= 400:          # skip splash / tool windows
                found.append((hwnd, buf.value))
        return True

    user32.EnumWindows(EnumWindowsProc(cb), 0)
    return found


def client_area(hwnd):
    """(left, top, width, height) of the client area, in screen coordinates."""
    r = wt.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(r))
    pt = wt.POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(pt))
    return pt.x, pt.y, r.right - r.left, r.bottom - r.top


def focus(hwnd):
    for _ in range(5):
        user32.ShowWindow(hwnd, 9)               # SW_RESTORE
        user32.SetForegroundWindow(hwnd)
        time.sleep(0.3)
        if user32.GetForegroundWindow() == hwnd:
            return True
        pyautogui.press("alt")                   # the classic unlock for SetForegroundWindow
    return user32.GetForegroundWindow() == hwnd


def shot(hwnd, name):
    os.makedirs(SHOTS, exist_ok=True)
    x, y, w, h = client_area(hwnd)
    path = os.path.join(SHOTS, f"{datetime.now():%H%M%S}-{name}.png")
    pyautogui.screenshot(region=(x, y, w, h)).save(path)
    return path


def click(hwnd, xy):
    x, y, _, _ = client_area(hwnd)
    pyautogui.click(x + xy[0], y + xy[1])


def type_text(text):
    pyautogui.write(text, interval=0.03)


class Client:
    def __init__(self):
        self.proc = None
        self.hwnd = None

    def launch(self):
        exe = os.path.abspath(CLIENT_EXE)
        if not os.path.exists(exe):
            raise SystemExit(f"client exe not found: {exe}")
        # The exe's manifest asks for administrator rights (a UAC prompt on every launch, which no
        # unattended run can click). RunAsInvoker makes Windows ignore that request, so the client
        # starts as the harness user; the harness itself may run elevated or not.
        env = dict(os.environ, __COMPAT_LAYER="RunAsInvoker")
        self.proc = subprocess.Popen([exe], cwd=os.path.dirname(exe), env=env)
        log(f"launched client pid {self.proc.pid}")
        deadline = time.time() + 60
        while time.time() < deadline:
            wins = windows_of_pid(self.proc.pid)
            if wins:
                self.hwnd = wins[0][0]
                log(f"client window found: hwnd {self.hwnd} '{wins[0][1]}'")
                return
            if self.proc.poll() is not None:
                raise SystemExit("the client exited before showing a window")
            time.sleep(1)
        raise SystemExit("no client window appeared within 60 s")

    def alive(self):
        if self.proc is None or self.proc.poll() is not None:
            return False
        return bool(user32.IsWindow(self.hwnd))

    def hung(self):
        return bool(user32.IsHungAppWindow(self.hwnd)) if self.hwnd else False

    def kill(self):
        if self.proc is None:
            return
        if self.proc.poll() is None:
            if self.hwnd:
                user32.PostMessageW(self.hwnd, 0x0010, 0, 0)   # WM_CLOSE first
                time.sleep(3)
            if self.proc.poll() is None:
                subprocess.run(["taskkill", "/F", "/PID", str(self.proc.pid)], capture_output=True)
                time.sleep(2)
            if self.proc.poll() is None:
                log("WARNING: the client process refused to die (anti-cheat?) — continuing anyway")
        self.proc = None
        self.hwnd = None


# ---------------------------------------------------------------------------------------------
# the login flow (positions above)
# ---------------------------------------------------------------------------------------------

def login(client):
    hwnd = client.hwnd
    log(f"waiting {LOGIN_WAIT}s for the login screen")
    time.sleep(LOGIN_WAIT)
    if not focus(hwnd):
        log("WARNING: could not bring the client to the foreground")
    shot(hwnd, "1-login")
    click(hwnd, LOGIN_ID_XY)
    pyautogui.hotkey("ctrl", "a")
    type_text(ACCOUNT)
    click(hwnd, LOGIN_PW_XY)
    type_text(PASSWORD)
    pyautogui.press("enter")
    time.sleep(STAGE_WAIT)
    shot(hwnd, "2-world")
    click(hwnd, WORLD_XY)
    time.sleep(1.5)
    click(hwnd, CHANNEL_XY)
    time.sleep(STAGE_WAIT)
    shot(hwnd, "3-chars")
    click(hwnd, CHAR_XY)
    time.sleep(1)
    click(hwnd, CHAR_SELECT_BTN_XY)
    time.sleep(ENTER_WAIT)
    shot(hwnd, "4-ingame")
    log("login sequence sent")


def chat(client, text):
    focus(client.hwnd)
    pyautogui.press("enter")
    time.sleep(0.4)
    type_text(text)
    time.sleep(0.2)
    pyautogui.press("enter")
    log(f"sent chat: {text}")


# ---------------------------------------------------------------------------------------------
# progress file helpers
# ---------------------------------------------------------------------------------------------

def progress_lines():
    if not os.path.exists(PROGRESS):
        return []
    with open(PROGRESS, encoding="utf-8", errors="replace") as f:
        return [l.rstrip("\n") for l in f]


def last_map():
    for line in reversed(progress_lines()):
        if line and not line.startswith("#"):
            parts = line.split("\t")
            return parts[0], (parts[1] if len(parts) > 1 else "")
    return None, None


def sweep_done():
    lines = progress_lines()
    return bool(lines) and lines[-1].startswith("# done")


def progress_mtime():
    return os.path.getmtime(PROGRESS) if os.path.exists(PROGRESS) else 0


def record_crash(reason):
    map_id, name = last_map()
    line = f"{datetime.now():%Y-%m-%d %H:%M:%S}\t{map_id}\t{name}\t{reason}"
    with open(CRASHES, "a", encoding="utf-8") as f:
        f.write(line + "\n")
    log(f"CRASH recorded: {line}")


# ---------------------------------------------------------------------------------------------
# modes
# ---------------------------------------------------------------------------------------------

def mode_probe():
    c = Client()
    c.launch()
    for i in range(14):
        time.sleep(5)
        if not c.alive():
            log("client gone")
            break
        p = shot(c.hwnd, f"probe-{i:02d}")
        log(f"shot {p}")
    log("probe done — look at DevTools/harness-shots and calibrate the *_XY constants")


def mode_stage(stage):
    """Calibration: run the flow up to `stage` (1 = after login, 2 = after world/channel) and keep shooting."""
    c = Client()
    c.launch()
    hwnd = c.hwnd
    time.sleep(LOGIN_WAIT)
    focus(hwnd)
    shot(hwnd, "s0-login")
    click(hwnd, LOGIN_ID_XY)
    pyautogui.hotkey("ctrl", "a")
    type_text(ACCOUNT)
    click(hwnd, LOGIN_PW_XY)
    type_text(PASSWORD)
    pyautogui.press("enter")
    if stage >= 2:
        time.sleep(STAGE_WAIT)
        shot(hwnd, "s1-world")
        click(hwnd, WORLD_XY)
        time.sleep(1.5)
        click(hwnd, CHANNEL_XY)
    for i in range(8):
        time.sleep(3)
        if not c.alive():
            log("client gone")
            break
        log("shot " + shot(hwnd, f"stage{stage}-{i:02d}"))


def mode_login():
    c = Client()
    c.launch()
    login(c)
    log("login mode done (client left running)")


def babysit(first_command, dwell):
    c = Client()
    c.launch()
    login(c)
    chat(c, first_command)
    stall_limit = dwell * STALL_FACTOR + 30
    last_mtime = progress_mtime()
    last_change = time.time()
    while True:
        time.sleep(3)
        if sweep_done():
            log("sweep reports '# done' — every map entered. Finished.")
            return
        mt = progress_mtime()
        if mt != last_mtime:
            last_mtime, last_change = mt, time.time()
        reason = None
        if not c.alive():
            reason = "client process exited (crash)"
        elif c.hung():
            time.sleep(15)
            if c.hung():
                reason = "client window hung"
        elif time.time() - last_change > stall_limit:
            reason = f"no progress for {int(time.time() - last_change)} s (disconnected?)"
        if reason is None:
            continue
        if c.hwnd and user32.IsWindow(c.hwnd):
            try:
                shot(c.hwnd, "crash")
            except Exception:
                pass
        record_crash(reason)
        c.kill()
        time.sleep(5)
        c = Client()
        c.launch()
        login(c)
        chat(c, "/sweep resume")
        last_mtime, last_change = progress_mtime(), time.time()


def main(argv):
    if len(argv) < 2 or argv[1] not in ("probe", "stage1", "stage2", "login", "run", "resume"):
        print(__doc__)
        return 1
    mode = argv[1]
    if mode == "probe":
        mode_probe()
    elif mode in ("stage1", "stage2"):
        mode_stage(int(mode[-1]))
    elif mode == "login":
        mode_login()
    elif mode == "run":
        rest = argv[2:]
        dwell = float(rest[2]) if len(rest) >= 3 else 3.0
        cmd = "/sweep maps" + ("".join(" " + a for a in rest[:3]))
        babysit(cmd, dwell)
    else:
        dwell = float(argv[2]) if len(argv) >= 3 else 3.0
        babysit(f"/sweep resume {argv[2]}" if len(argv) >= 3 else "/sweep resume", dwell)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
