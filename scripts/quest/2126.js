// 砂絵団の補給品！ (quest 2126, 完了 NPC 2101002) — 箱(4031624)を届ける。出典 Reference/Cosmic/scripts/quest/2126.js。
// Cosmic は 4031619 を見るが、JMS v186 の Check1 が要求する箱は 4031624(Act0 で渡される)なので JMS に合わせた。終了スクリプト q2126e。
function end() {
    if (!player.haveItem(4031624)) {
        qm.sendOk("#b#p2012019##k から預かった補給品の箱を持ってきておくれ…");
        return;
    }
    player.gainItem(4031624, -1);
    qm.sendOk("おお、#p2012019# の箱を届けてくれたのか！　ありがとう。");
    player.completeQuest(2126);
}
