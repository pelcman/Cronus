// 過去への道 (quest 21749, NPC 1002104 トゥルー, Lv63) — 二つの封印石を失った今、時間の門で過去へ向かう話。その場で受注→完了(EXP 500 は Act)。
// 出典 Reference/Cosmic/scripts/quest/21749.js。JMS: 開始スクリプト q21749s、normalAutoStart。台詞は創作。
function start() {
    qm.sendNext("これで #b封印石を二つ#k 失ったことになる。次に狙われるのは、どこの封印石か…。");
    qm.sendNext("アラン、次の任務だ。#b時間の門#k を使って過去へ渡り、封印石の記録を持つ者に会ってほしい。");
    player.startQuest(21749);
    player.completeQuest(21749);
}
