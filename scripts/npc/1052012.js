// ネットカフェ案内 — ネットカフェへの入場 (無料)
var CAFE = 193000000;
function start() {
    if (player.getMapId() == CAFE) {
        cm.sendOk("ごゆっくりどうぞ。お帰りはポータルからどうぞ。");
        return;
    }
    if (cm.askYesNo("ここから先はネットカフェ会員専用のくつろぎスペースです。入場しますか?")) {
        player.warp(CAFE);
    }
}
