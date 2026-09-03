// プリン (控え室<オルビス行き>) — 出発待ちの案内。降りる場合はチケットを返して駅へ。
var TICKET = 4031045;
function start() {
    var mins = player.airshipMinutes();
    var pick = cm.askMenu("こちらはオルビス行きの控え室です。出発時刻になったら自動で飛行船へご案内します。"
        + (mins > 0 ? "\r\n出発まであと約#b" + mins + "分#k。" : "\r\n#bまもなく出発です。#k")
        + "\r\n#L0#待つ#l"
        + "\r\n#L1#やっぱり降りる (チケットを返してもらう)#l");
    if (pick == 1) {
        player.gainItem(TICKET, 1);
        player.warp(101000300);
    }
}
