// 失った思い出を探して (quest 3539, NPC 1201000 リリン, アラン) — 思い出の観照者(3507)の進行中に自分の転職官へ話すと記憶が戻る。
// JMS: 3507 の完了条件は infoNumber=7081 の情報が "1" なので(Cosmic の setQuestProgress(3507, 7081, 1) と同じ意味)、7081 の記録に "1" を
// 立て、このクエスト自身はその場で受注→完了する。JMS: 開始スクリプト q3539s、Act は空。出典 Reference/Cosmic/scripts/quest/3539.js。台詞は創作。
function start() {
    qm.sendOk("…そうだ、思い出した。失っていた記憶が戻ってきた。#b#p2140001##k のところへ戻れば、通行証をもらえるはずだ。");
    player.setQuestData(7081, "1");
    player.startQuest(3539);
    player.completeQuest(3539);
}
