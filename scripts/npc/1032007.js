// ジョエル (エリニアステーション, wz script = sell_ticket) — オルビス行き飛行船のチケット販売。
// 飛行船: 15分周期(乗船10分/飛行5分)の運航はサーバーのスケジューラが担う(Airship.cs)。価格は簡易仕様(創作)。
var TICKET = 4031045;   // オルビス行きのチケット(大人用)
var PRICE = 5000;
function start() {
    var pick = cm.askMenu("エリニアステーションへようこそ。オルビス行き飛行船のチケットはこちらで。"
        + "\r\n#L0#オルビス行きのチケットを買う (" + PRICE + "メル)#l"
        + "\r\n#L1#次の便はいつ?#l");
    if (pick == 0) {
        if (player.haveItem(TICKET)) {
            cm.sendOk("チケットはもうお持ちですよ。#b乗船係のチェリ#kに見せて乗ってくださいね。");
            return;
        }
        if (player.getMeso() < PRICE) {
            cm.sendOk("メルが足りないようです。チケットは" + PRICE + "メルです。");
            return;
        }
        player.gainMeso(-PRICE);
        player.gainItem(TICKET, 1);
        cm.sendOk("#b#t" + TICKET + "##kをどうぞ。乗船は隣の#bチェリ#kへ。出発時刻にご注意を!");
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
