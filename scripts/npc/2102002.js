// シラス (アリアント乗降場, wz script = sell_ticket) — オルビス行きチケット(4031045)の販売。
// 価格は OdinMS 系スクリプト値(6000メル、創作)、台詞は日本語化(創作)。
var TICKET = 4031045;
var PRICE = 6000;
function start() {
    if (player.haveItem(TICKET)) {
        cm.sendOk("#b#t" + TICKET + "##kはもうお持ちですね。乗船係の#bアセソン#kにお見せください。");
        return;
    }
    if (!cm.askYesNo("オシリア大陸のオルビスステーション行きの船のチケットを販売しています。料金は#b" + PRICE + "メル#kです。#b#t" + TICKET + "##kを購入しますか？")) return;
    if (player.getMeso() < PRICE) {
        cm.sendOk("メルが足りないようです。その他(ETC)欄の空きもご確認ください。");
        return;
    }
    player.gainMeso(-PRICE);
    player.gainItem(TICKET, 1);
    cm.sendOk("#b#t" + TICKET + "##kをどうぞ。乗船は#bアセソン#kへ。");
}
