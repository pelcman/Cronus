// 砂絵団の補給品 (quest 2124, 完了 NPC 2101002) — ボンちゃんの小さい箱(4031619)を届ける。出典 Reference/Cosmic/scripts/quest/2124.js。
// JMS Check1 の要求アイテムは 4031619(Act0 で渡される)。終了スクリプト q2124e。台詞は創作。
function end() {
    if (!player.haveItem(4031619)) {
        qm.sendOk("#b#p2012019##k から預かった補給品の箱を持ってきておくれ…");
        return;
    }
    player.gainItem(4031619, -1);
    qm.sendOk("おお、#p2012019# の箱を届けてくれたのか！　ありがとう。");
    player.completeQuest(2124);
}
