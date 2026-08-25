// イルカ (アクアリウム, wz script = aqua_taxi) — アクアロードの送迎イルカ。
// 行き先と料金は簡易仕様(創作)。マップIDは実データ検証済み。
function start() {
    var spots = [
        ["アクアリウム", 230000000, 1000],
        ["海の道", 230010000, 1000],
        ["巨大魚の洞窟", 230040420, 10000],
        ["白草村", 251000000, 10000]
    ];
    var menu = "キュイッ!(どこへ運んでほしいの?)";
    for (var i = 0; i < spots.length; i++) {
        menu += "\r\n#L" + i + "#" + spots[i][0] + " (" + spots[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= spots.length) return;
    var s = spots[pick];
    if (player.getMapId() == s[1]) {
        cm.sendOk("キュイ?(もうここだよ?)");
        return;
    }
    if (player.getMeso() < s[2]) {
        cm.sendOk("キュゥ…(" + s[2] + "メル持ってきてね)");
        return;
    }
    player.gainMeso(-s[2]);
    player.warp(s[1]);
}
