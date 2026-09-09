// もう一つの封印石の情報 (quest 21754, 受注 NPC 1012100 ヘレナ → 完了 NPC 1002104 トゥルー, Lv63) — ヘレナが長く保管していた古い手紙(4032328)をトゥルーへ。
// 手紙の付与はスクリプト側(JMS の Act[0] は空; Act[1] で回収・EXP 56800)。出典 Reference/Cosmic/scripts/quest/21754.js。JMS: 開始スクリプト q21754s、
// Check は 21753 完了 + 隠し記録 21765 = "2"。台詞は創作。
function start() {
    qm.sendNext("これを持って行きなさい。#r#p1002104##k へ。封印石について、私が長い間預かっていた手紙です。");
    if (!player.haveItem(4032328)) {
        player.gainItem(4032328, 1);
    }
    player.startQuest(21754);
}
