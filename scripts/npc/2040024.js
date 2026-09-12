// 一番目のエオス石 2040024 (エオス塔100階 221024400, JMS スクリプト名 ludi014) — エオス石の書(4001020)を 1 枚使って 二番目のエオス石(71階 221022900) へ飛ぶ。
// 出典 Reference/Cosmic/scripts/npc/2040024.js。台詞は創作。
function start() {
    if (!player.haveItem(4001020)) {
        cm.sendOk("#b二番目のエオス石#k へ飛べる石があるが、#bエオス石の書#k が無ければ発動できない。");
        return;
    }
    if (!cm.askYesNo("#bエオス石の書#k を使って #b一番目のエオス石#k を発動できる。71 階の #b二番目のエオス石#k へ飛ぶか？")) {
        return;
    }
    player.gainItem(4001020, -1);
    player.warp(221022900, 3);
}
