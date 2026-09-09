// 騎士の軌跡を追って (quest 20400, NPC 1101002 ナインハート) — Lv120 の騎士に、行方不明の上級騎士の捜索を頼む導入。話を聞くとその場で受注→完了し、
// 続く 20401 ゾンビ倒し(ゼイド)へ。出典 Reference/Cosmic/scripts/quest/20400.js。JMS: 開始スクリプト q20400s、normalAutoStart、Lv120、Act は空。台詞は創作。
function start() {
    qm.sendNext("少し前、#b上級騎士 #p1103000##k から救難信号が届いた。エルナスの方角からだ。そなたに調べてきてもらいたい。まずはエルナスの #bゼイド#k を訪ねなさい。");
    player.startQuest(20400);
    player.completeQuest(20400);
}
