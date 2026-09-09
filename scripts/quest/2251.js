// ゾンビキノコの信号体系3 (quest 2251, NPC 1061011 修行者) — 音を入れる符籍(4032399)を 20 枚届ける。
// 出典 Reference/Cosmic/scripts/quest/2251.js。JMS: 終了スクリプト q2251e、Check1 要求 4032399×20、Act1 で EXP 8000(完了処理で付与)。台詞は創作。
function end() {
    if (player.itemQuantity(4032399) < 20) {
        qm.sendOk("#b#t4032399##k を 20 枚持ってきてくれ…　#i4032399#");
        return;
    }
    player.gainItem(4032399, -20);
    qm.sendOk("おお、#b#t4032399##k を 20 枚集めてきたのか！　ありがとう。");
    player.completeQuest(2251);
}
