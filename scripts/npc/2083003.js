// 迷路部屋の切り株 (迷路部屋, wz script = hontale_Bdoor) — 迷路の抜け道。
function start() {
    if (cm.askYesNo("切り株の裏に隠し通路がある。#b選択の洞窟#kへ抜けられそうだ。進むか?")) {
        player.warp(240050200);
    }
}
