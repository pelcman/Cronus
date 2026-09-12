// 子牛 1092095 (ノーチラスの牛小屋 912000100, JMS スクリプト名 baby_cow) — 子牛は牛乳を飲んでしまう: 途中まで満たしたボトルは空(4031847)に戻る。
// 出典 Reference/Cosmic/scripts/npc/1092095.js。台詞は創作。
function start() {
    if (player.haveItem(4031848) || player.haveItem(4031849) || player.haveItem(4031850)) {
        cm.sendOk("お腹を空かせた子牛が牛乳を全部飲んでしまった！　ボトルは空になった。");
        if (player.haveItem(4031848)) {
            player.gainItem(4031848, -1);
        } else if (player.haveItem(4031849)) {
            player.gainItem(4031849, -1);
        } else {
            player.gainItem(4031850, -1);
        }
        player.gainItem(4031847, 1);
        return;
    }
    if (player.haveItem(4031847)) {
        cm.sendOk("お腹を空かせた子牛は、空のボトルには興味がないようだ。");
        return;
    }
    cm.sendOk("子牛が母牛のそばで甘えている。");
}
