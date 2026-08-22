// クロート — 「一つ目の同行」ステージ案内 (簡略版: 各自クリアで次のステージへ)
var COUPON = 4001007; // リゲーターのクーポン
var PASS = 4001008;   // ボスのパス
function start() {
    var map = player.getMapId();
    if (map == 103000800) {
        var n = player.itemQuantity(COUPON);
        if (n < 20) {
            cm.sendOk("リゲーターを倒してクーポンを20枚集めてくるんだ。今は" + n + "枚だな。");
            return;
        }
        if (cm.askYesNo("クーポン20枚、確かに受け取った。次のステージへ進むか?")) {
            player.gainItem(COUPON, -20);
            player.gainExp(1500);
            player.warp(103000801);
        }
        return;
    }
    if (map == 103000801 || map == 103000802 || map == 103000803) {
        var next = map + 1;
        if (cm.askYesNo("このステージの仕掛けはもう解いてある(簡略版)。次へ進むか?")) {
            player.gainExp(1500);
            player.warp(next);
        }
        return;
    }
    if (map == 103000804) {
        if (!player.haveItem(PASS)) {
            cm.sendOk("キングスライムを倒してパスを手に入れるんだ!");
            return;
        }
        player.gainItem(PASS, -1);
        player.gainExp(3000);
        var pick = cm.askMenu("見事だ!「一つ目の同行」クリアおめでとう!"
            + "\r\n#L0#ボーナスステージへ#l"
            + "\r\n#L1#カニングシティへ帰る#l");
        player.warp(pick == 0 ? 103000805 : 103000000);
        return;
    }
    cm.sendOk("ここでは案内することがないな。");
}
