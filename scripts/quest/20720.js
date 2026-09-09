// ペリオン派遣任務開始前 (quest 20720, NPC 1101002 ナインハート) — Lv25 の騎士にペリオン派遣任務の開始を告げる。承諾で受注(完了側はデータ経路)。
// 出典 Reference/Cosmic/scripts/quest/20720.js。JMS: 開始スクリプト q20720s、normalAutoStart、Check は 20719 完了・Lv25、Act は空。台詞は創作。
function start() {
    if (!qm.askAccept("レベル上げは順調か？　そろそろ #bペリオン#k への派遣任務を任せられそうだ。受けてくれるか？")) {
        return;
    }
    player.startQuest(20720);
}
