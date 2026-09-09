// 女王の宰相、ナインハート (quest 21757, 受注 NPC 1201000 リリン → 完了 NPC 1101002 ナインハート, Lv68) — リリンの手紙(4032330)をエレヴの宰相へ届ける。
// 手紙の回収と EXP 1000 は JMS の Act。出典 Reference/Cosmic/scripts/quest/21757.js。JMS: 終了スクリプト q21757e、完了は 4032330×1。台詞は創作。
function end() {
    qm.sendNext("おお、#r女王陛下#k への手紙か？　#b英雄#k からだと？！　…なるほど、ブラックウイングの件だな。確かに受け取った。");
    player.completeQuest(21757);
}
