// 木の根の水晶 (迷路部屋群, wz script = hontale_out) — 迷路からの脱出装置。
function start() {
    if (cm.askYesNo("木の根に埋まった水晶が淡く光っている。触れると#b洞窟の入口#kへ戻れそうだ。戻るか?")) {
        player.warp(240050000);
    }
}
