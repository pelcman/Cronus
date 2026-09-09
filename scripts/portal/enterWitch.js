// 死んだ龍の巣 240040510 → 暗黒の魔女の洞窟 (924010000 / 消えた騎士 20406 完了後 924010100 / 暗黒の魔女の呪い 20407 完了後 924010200, ポータル 1)。
// 卵誘拐事件(20404)を終えた騎士だけが入れる。出典 Reference/Cosmic/scripts/portal/enterWitch.js。
function start() {
    if (!player.isQuestDone(20404)) {
        player.message("ここには入るべきではない…不気味だ。");
        return;
    }
    var map = 924010000;
    if (player.isQuestDone(20407)) {
        map = 924010200;
    } else if (player.isQuestDone(20406)) {
        map = 924010100;
    }
    player.warp(map, 1);
}
