// イス (オルビスチケット売場, wz script = getAboard) — エリニア行き飛行船の乗船係。
// 乗船受付中はチケットと引き換えに控え室へ。出発はサーバーのスケジューラが行う。
var TICKET = 4031047;   // エリニア行きのチケット(大人用)
function start() {
    if (!player.airshipBoarding()) {
        cm.sendOk("ただいま飛行船は航海中です。次の便は#b約5分後#kに乗船受付を始めます。");
        return;
    }
    if (!player.haveItem(TICKET)) {
        cm.sendOk("乗船には#b#t" + TICKET + "##kが必要です。隣の#bイフ#kから買えますよ。");
        return;
    }
    if (!cm.askYesNo("エリニア行きの飛行船に乗船しますか?出発まであと約#b" + player.airshipMinutes() + "分#kです。"
        + "\r\n#r出発後は途中で降りられません。#k")) {
        return;
    }
    player.gainItem(TICKET, -1);
    player.warp(200000112);
}
