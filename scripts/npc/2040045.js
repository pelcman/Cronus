// ピンクバルーン — LPQボーナスステージからの退場
function start() {
    if (cm.askYesNo("時間になったら戻らないとね。エオス塔101階へ帰る?")) {
        player.warp(221024500);
    }
}
