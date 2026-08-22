// LPQバルーン — ステージ判定 (簡略版: 各自クリアで次へ。同じスクリプトを全バルーンで共用)
var PASS = 4001022;   // 玩具の通行証
var PASS7 = 4001156;  // ステージ7の通行証
function start() {
    var map = player.getMapId();
    var stages = {
        922010100: [PASS, 15, 922010200],
        922010200: [0, 0, 922010300],
        922010300: [PASS, 8, 922010400],
        922010400: [0, 0, 922010500],
        922010500: [0, 0, 922010600],
        922010600: [0, 0, 922010700],
        922010700: [PASS7, 5, 922010800],
        922010800: [0, 0, 922010900],
        922010900: [0, 0, 922011100]
    };
    var st = stages[map];
    if (!st) {
        cm.sendOk("……(バルーンはぷかぷか浮いている)");
        return;
    }
    var item = st[0], need = st[1], next = st[2];
    if (item != 0) {
        var n = player.itemQuantity(item);
        if (n < need) {
            cm.sendOk("このステージのモンスターから通行証を" + need + "枚集めてね。今は" + n + "枚だよ。");
            return;
        }
        if (!cm.askYesNo("通行証" + need + "枚、確かに受け取ったよ。次のステージへ進む?")) {
            return;
        }
        player.gainItem(item, -need);
    } else if (!cm.askYesNo("このステージの仕掛けはもう解いてあるよ(簡略版)。次へ進む?")) {
        return;
    }
    player.gainExp(2500);
    player.warp(next);
}
