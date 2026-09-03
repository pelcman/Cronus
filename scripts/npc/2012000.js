// イフ (オルビスチケット売場, wz script = sell_ticket) — エリニア行き飛行船のチケット販売。
// 価格は簡易仕様(創作)。運航はサーバーのスケジューラ(Airship.cs)。
var TICKET = 4031047;   // エリニア行きのチケット(大人用)
var PRICE = 5000;
function start() {
    var pick = cm.askMenu("オルビスチケット売場です。エリニア行きの飛行船チケットを販売しています。"
        + "\r\n#L0#エリニア行きのチケットを買う (" + PRICE + "メル)#l"
        + "\r\n#L1#次の便はいつ?#l");
    if (pick == 0) {
        if (player.haveItem(TICKET)) {
            cm.sendOk("チケットはもうお持ちですね。#b乗船係のイス#kにお見せください。");
            return;
        }
        if (player.getMeso() < PRICE) {
            cm.sendOk("メルが足りません。チケットは" + PRICE + "メルです。");
            return;
        }
        player.gainMeso(-PRICE);
        player.gainItem(TICKET, 1);
        cm.sendOk("#b#t" + TICKET + "##kをどうぞ。乗船は隣の#bイス#kへ。");
        return;
    }
    if (pick == 1) {
        if (player.airshipBoarding()) {
            cm.sendOk("ただいま乗船受付中です。出発まであと約#b" + player.airshipMinutes() + "分#k。");
        } else {
            cm.sendOk("飛行船はただいま航海中です。次の便は#b約5分後#kに乗船受付を始めます。");
        }
    }
}
