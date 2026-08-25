// ホーンテイルの道標 (洞窟の入口, wz script = hontale_enter1) — 迷路部屋へ入る。
// 本来の遠征隊システムは未実装のため、ランダムな迷路部屋への入場として簡易実装。
var ROOMS = [240050100, 240050101, 240050102, 240050103, 240050104, 240050105];
function start() {
    if (!cm.askYesNo("この先はホーンテイルの巣へ続く迷路だ。#r一度入ると迷いやすい#k。"
        + "\r\n迷ったら#b木の根の水晶#kに触れれば入口へ戻れる。進むか?")) {
        return;
    }
    player.warp(ROOMS[Math.floor(Math.random() * ROOMS.length)]);
}
