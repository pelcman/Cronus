// 封印の庭園への道  (quest 21739, NPC 2032001 スピルナ, Lv45) — 巨大ネペンデス(9300348)を倒して報告。EXP 29500 は Cosmic の値(JMS の Act は空)。
// 出典 Reference/Cosmic/scripts/quest/21739.js。JMS: 終了スクリプト q21739e、完了は 9300348×1。台詞は創作。
function end() {
    qm.sendNext("それで、巨大ネペンデスは倒したのかい？　おや…ブラックウイングの手先が絡んでいたとは。封印の庭園への道が、これで開ける。");
    player.completeQuest(21739);
    player.gainExp(29500);
}
