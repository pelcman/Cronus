// 三番目のエオス石 2040026 (エオス塔41階 221021700, JMS スクリプト名 ludi016) — エオス石の書(4001020)を 1 枚使って 二番目(71階 221022900) か 四番目(1階 221020000)
// のエオス石へ飛ぶ。出典 Reference/Cosmic/scripts/npc/2040026.js。台詞は創作。
function start() {
    if (!player.haveItem(4001020)) {
        cm.sendOk("#b二番目か四番目のエオス石#k へ飛べる石があるが、#bエオス石の書#k が無ければ発動できない。");
        return;
    }
    var pick = cm.askMenu("#bエオス石の書#k を使って #b三番目のエオス石#k を発動できる。どちらの石へ飛ぶ？#b\r\n#L0#二番目のエオス石 (71階)#l\r\n#L1#四番目のエオス石 (1階)#l");
    if (pick != 0 && pick != 1) {
        return;
    }
    var map = pick == 0 ? 221022900 : 221020000;
    if (!cm.askYesNo(pick == 0 ? "71 階の #b二番目のエオス石#k へ飛ぶか？" : "1 階の #b四番目のエオス石#k へ飛ぶか？")) {
        return;
    }
    player.gainItem(4001020, -1);
    player.warp(map, pick == 0 ? 3 : 4);
}
