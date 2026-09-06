// イフ (オルビスチケット売場, wz script = sell_ticket) — 各地行きの船のチケット販売。
// 台詞と4路線は JMS 原文(Riremito/jms_scripts より)。価格はオラクルに値が無いため GMS 相当の簡易値(創作)。チケットIDは実データ検証済み。
var TICKETS = [
    ["ビクトリアアイランドのエリニア", 4031047, 5000, "リニ"],
    ["ルディブリアム城", 4031074, 6000, "スナ"],
    ["リプレ村", 4031331, 30000, "ラミニ"],
    ["アリアント", 4031576, 6000, "ゼラス"]
];
function start() {
    var menu = "私は各地域に行く船のチケットを売っています。どのチケットを購入しますか？";
    for (var i = 0; i < TICKETS.length; i++) {
        menu += "\r\n#L" + i + "##b" + TICKETS[i][0] + "#k (" + TICKETS[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= TICKETS.length) return;
    var t = TICKETS[pick];
    if (player.haveItem(t[1])) {
        cm.sendOk("#b#t" + t[1] + "##kはもうお持ちですね。昇降場の#b" + t[3] + "#kにお見せください。");
        return;
    }
    if (!cm.askYesNo("#b#t" + t[1] + "##kを" + t[2] + "メルで購入しますか？")) return;
    if (player.getMeso() < t[2]) {
        cm.sendOk("メルが足りないようです。チケットは" + t[2] + "メルです。");
        return;
    }
    player.gainMeso(-t[2]);
    player.gainItem(t[1], 1);
    cm.sendOk("#b#t" + t[1] + "##kをどうぞ。昇降場へは#bイス#kが案内します。乗船は昇降場の#b" + t[3] + "#kへ。");
}
