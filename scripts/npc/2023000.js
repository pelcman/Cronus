// 危険地域弾丸タクシー (エルナス/ルディブリアム/リプレ, wz script = ossyria_taxi) — 各町からダンジョン入口への直行タクシー。
// 台詞と行き先は JMS 原文(Riremito/jms_scripts より): エルナス→氷の谷2、ルディブリアム→時間の通路、リプレ→龍の森の入口(町ごとに1か所)。
// 料金はオラクルに値が無いため簡易(創作: 6000メル)。
var ROUTES = [
    [211000000, 211040200],
    [220000000, 220050300],
    [240000000, 240030000]
];
var FARE = 6000;
function start() {
    var here = player.getMapId();
    var dest = 0;
    for (var i = 0; i < ROUTES.length; i++) {
        if (ROUTES[i][0] == here) dest = ROUTES[i][1];
    }
    if (dest == 0) {
        cm.sendOk("ここからの運行はしていません。エルナス、ルディブリアム、リプレの町でお待ちしています。");
        return;
    }
    if (!cm.askYesNo("こんにちは！ダンジョン行きの特急タクシーです。#m" + here + "#に乗って#b#m" + dest + "##kに移動しますか。費用は#b" + FARE + "メル#kです。")) return;
    if (player.getMeso() < FARE) {
        cm.sendOk("メルが足りないようです。料金は" + FARE + "メルです。");
        return;
    }
    player.gainMeso(-FARE);
    player.warp(dest);
}
