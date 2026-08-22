// 極楽 — 回復サウナ室<一般>への案内 (料金制)
var COST = 500;
var SAUNA = 105040401;
function start() {
    if (!cm.askYesNo("いらっしゃい!回復サウナ室<一般>はどうだい?中にいるだけで体力がぐんぐん回復するよ。"
            + "入場料は" + COST + "メルだ。入るかい?")) {
        cm.sendOk("また疲れたときにでも寄っていっておくれ。");
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("おっと、メルが足りないようだね。入場料は" + COST + "メルだよ。");
        return;
    }
    player.gainMeso(-COST);
    player.warp(SAUNA);
}
