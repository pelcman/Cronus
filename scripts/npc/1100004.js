// キル (エレヴ ステーション, wz script = contimoveEreOrb) — エレヴ→オルビス(ステーション<エレヴ行き> 200000161)の船。行き先はここのみ。
// 台詞は JMS 原文(Riremito/jms_scripts より)。運航は直行ワープ(実飛行化はフェーズ3の残件)。料金1000メル。
var DEST = 200000161;
var FARE = 1000;
function start() {
    if (!cm.askYesNo("オシリア大陸の#bオルビス#kまでかかる時間は約 #b8分#kだ。料金は#b1000#kメル。1000メル出して船に乗るか？")) return;
    if (player.getMeso() < FARE) {
        cm.sendOk("メルが足りないようだな。#b1000メル#k用意してから来てくれ。");
        return;
    }
    player.gainMeso(-FARE);
    player.warp(DEST);
}
