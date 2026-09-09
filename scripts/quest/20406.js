// 消えた騎士 (quest 20406, NPC 1101002 ナインハート) — 洞窟のメモを報告する。話すとその場で受注→完了。
// 出典 Reference/Cosmic/scripts/quest/20406.js。JMS: 開始スクリプト q20406s、Check は 20405 完了・Lv120、Act は空。台詞は創作。
function start() {
    qm.sendNext("そうか…#p1103000# が呪いの源を追って先へ進んだ、とだけ書き残していたのだな。装置がエレヴに届いているか確かめよう。そなたは女王陛下に報告に行きなさい。");
    player.startQuest(20406);
    player.completeQuest(20406);
}
