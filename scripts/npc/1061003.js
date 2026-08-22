// 超極楽 — 回復サウナ室<高級>への案内 (料金制)
var COST = 1500;
var SAUNA = 105040402;
function start() {
    if (!cm.askYesNo("いらっしゃい!こちらは回復サウナ室<高級>。回復の効きが段違いさ。"
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
