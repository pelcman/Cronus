// ゾンビ倒し (quest 20401, NPC 2020006 ゼイド, エルナス) — 上級騎士の足取りを聞く。話を聞くとその場で受注→完了。
// 出典 Reference/Cosmic/scripts/quest/20401.js。JMS: 開始スクリプト q20401s、Check は 20400 完了・Lv120、Act は空。台詞は創作。
function start() {
    qm.sendNext("#b上級騎士 #p1103000##k を最後に見たのは、雪の洞窟の奥へ向かうところだった。ゾンビどもの巣だ…無事だといいが。跡を追うなら、気をつけて行きなさい。");
    player.startQuest(20401);
    player.completeQuest(20401);
}
