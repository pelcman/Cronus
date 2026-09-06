// イス (オルビスチケット売場, wz script = getAboard) — 各昇降場(ステーション通路)への案内。行き先はこの6マップに限定。
// 台詞と昇降場一覧は JMS 原文(Riremito/jms_scripts より)。到着ポータルは west00(原文どおり)。
var PLATFORMS = [
    [200000110, "エリニア行きの船に乗る昇降場"],
    [200000120, "ルディブリアム行きの船に乗る昇降場"],
    [200000130, "リプレ行きの船に乗る昇降場"],
    [200000140, "武陵行きのツルに乗る昇降場"],
    [200000150, "アリアント行きのジニに乗る昇降場"],
    [200000160, "エレヴ行きの船に乗る昇降場"]
];
function start() {
    var menu = "オルビスステーションには多くの昇降場があります。目的地によって正しい昇降場へ行かなければなりません。どの方面に向かう船の昇降場に行くんですか？";
    for (var i = 0; i < PLATFORMS.length; i++) {
        menu += "\r\n#L" + i + "##b" + PLATFORMS[i][1] + "#k#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= PLATFORMS.length) return;
    player.warpPortal(PLATFORMS[pick][0], "west00");
}
