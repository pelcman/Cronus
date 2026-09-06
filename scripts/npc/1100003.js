// キリル (エレヴ ステーション, wz script = contimoveEreEli) — エレヴ→エリニア(ステーション<エレヴ行き> 101000400)の船。行き先はここのみ。
// 台詞は Riremitoさん版(キルの原文に合わせた文)。運航は直行ワープ(実飛行化はフェーズ3の残件)。料金1000メル。
var DEST = 101000400;
var FARE = 1000;
function start() {
    if (!cm.askYesNo("#bエリニア#kまでかかる時間は約 #b8分#kだ。料金は#b1000#kメル。1000メル出して船に乗るか？")) return;
    if (player.getMeso() < FARE) {
        cm.sendOk("メルが足りないようだな。#b1000メル#k用意してから来てくれ。");
        return;
    }
    player.gainMeso(-FARE);
    player.warp(DEST);
}
