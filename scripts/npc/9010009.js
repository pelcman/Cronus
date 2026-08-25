// ドイ (宅配, wz info parcel=1) — ホームデリバリーの受付。
// 発送は本物の宅配UI(送信レイアウトは実クライアントのワイヤキャプチャから解読)。
// 受取はこの窓口で手渡し(受信UIのバイト仕様が未解明のため)。
function start() {
    var waiting = player.parcelCount();
    var menu = "こんにちは、宅配のドイです。";
    if (waiting > 0) {
        menu += "\r\n#e#rお客様宛の宅配物が" + waiting + "件届いていますよ!#k#n";
    }
    menu += "\r\n#L0#宅配を送る#l";
    if (waiting > 0) {
        menu += "\r\n#L1#宅配物を受け取る (" + waiting + "件)#l";
    }
    menu += "\r\n#L2#宅配って何?#l";
    var pick = cm.askMenu(menu);
    if (pick == 0) {
        player.openParcel();
        return;
    }
    if (pick == 1) {
        var got = player.receiveParcels();
        var left = player.parcelCount();
        if (got <= 0) {
            cm.sendOk("インベントリに空きが無いようです。整理してからまたお越しください。");
        } else if (left > 0) {
            cm.sendOk(got + "件お渡ししました!残り" + left + "件はインベントリの空きが足りません。"
                + "\r\n整理してからまた声をかけてくださいね。");
        } else {
            cm.sendOk(got + "件、全部お渡ししました!またのご利用をお待ちしています。");
        }
        return;
    }
    if (pick == 2) {
        cm.sendOk("宅配は他のキャラクターへアイテムやメルを送れるサービスです。"
            + "\r\n送るときは#b宅配を送る#kから、届いた荷物は#b私に話しかけて受け取り#kですよ。");
    }
}
