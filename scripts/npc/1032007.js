// ジョエル (エリニアステーション) — オルビス行き飛行船のチケット販売。
// 飛行船: 15分周期(乗船10分/飛行5分)の運航はサーバーのスケジューラが担う(Airship.cs)。
// 価格は簡易仕様(創作)。身内サーバー向けに、待たずに移動できる直行便の案内も残している。
var TICKET = 4031045;   // オルビス行きのチケット(大人用)
var PRICE = 5000;
function start() {
    var pick = cm.askMenu("エリニアステーションへようこそ。オルビス行き飛行船のチケットはこちらで。"
        + "\r\n#L0#オルビス行きのチケットを買う (" + PRICE + "メル)#l"
        + "\r\n#L1#次の便はいつ?#l"
        + "\r\n#L2#急ぎなので直行便を使う (各地へ即時移動)#l");
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
        return;
    }
    if (pick == 2) {
        directService();
    }
}
function directService() {
    var places = [
        ["ヘネシス (ビクトリア)", 100000000, 1000],
        ["リス港 (ビクトリア)", 104000000, 1000],
        ["オルビス", 200000000, 5000],
        ["エルナス", 211000000, 5000],
        ["ルディブリアム", 220000000, 5000],
        ["アクアリウム", 230000000, 5000],
        ["リプレ", 240000000, 5000],
        ["ムーラン", 250000000, 5000],
        ["薬草町", 251000000, 5000],
        ["アリアント", 260000000, 5000],
        ["マガティア", 261000000, 5000],
        ["時間の神殿", 270000100, 8000],
        ["きのこ神社 (ジパング)", 800000000, 8000],
        ["エレブ (シグナス騎士団)", 130000000, 5000],
        ["リエン (アランの雪原)", 140000000, 5000]
    ];
    var menu = "直行便はどちらへ?運賃は前払いです。";
    for (var i = 0; i < places.length; i++) {
        menu += "\r\n#L" + i + "#" + places[i][0] + " (" + places[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= places.length) return;
    var dest = places[pick];
    if (player.getMapId() == dest[1]) {
        cm.sendOk("もうそこにいらっしゃいますよ。");
        return;
    }
    if (player.getMeso() < dest[2]) {
        cm.sendOk("メルが足りないようです。運賃は" + dest[2] + "メルです。");
        return;
    }
    if (cm.askYesNo(dest[0] + "行きは" + dest[2] + "メルです。よろしいですか?")) {
        player.gainMeso(-dest[2]);
        player.warp(dest[1]);
    }
}
