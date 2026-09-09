// まだ終わらない修行 (quest 20610, NPC 1101002 ナインハート) — Lv110 の騎士に続きの修行を命じる。承諾で受注(完了側はデータ経路)。
// 出典 Reference/Cosmic/scripts/quest/20610.js。JMS: 開始スクリプト q20610s、normalAutoStart、Lv110、Act は空。台詞は創作。
function start() {
    if (!qm.askAccept("スキルは磨き続けているか？　これまでの修行で、そなたの技はかなり練り上げられたはずだ。もう一段、続けてみるか？")) {
        qm.sendOk("ふむ…今のそなたは、騎士団長を目指す者には見えないな。気が変わったら来なさい。");
        return;
    }
    player.startQuest(20610);
}
