// 危険地域弾丸タクシー (エルナス, wz script = ossyria_taxi) — 雪原の危険地帯への送迎。
// 行き先と料金は簡易仕様(創作)。マップIDは実データ検証済み。
function start() {
    var spots = [
        ["エルナス (村)", 211000000, 1000],
        ["冷気の平原2", 211040000, 6000],
        ["険しき絶壁1", 211040300, 6000],
        ["死んだ木の森4", 211041400, 6000],
        ["試練の洞窟1", 211042000, 6000],
        ["ジャクムの祭壇入口", 211042400, 10000]
    ];
    var menu = "危険地域までひとっ走り、弾丸タクシーだ!どこまで行く?";
    for (var i = 0; i < spots.length; i++) {
        menu += "\r\n#L" + i + "#" + spots[i][0] + " (" + spots[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= spots.length) return;
    var s = spots[pick];
    if (player.getMapId() == s[1]) {
        cm.sendOk("もうそこだぜ、旦那。");
        return;
    }
    if (player.getMeso() < s[2]) {
        cm.sendOk("料金は前払い、" + s[2] + "メルだ。足りないぜ。");
        return;
    }
    player.gainMeso(-s[2]);
    player.warp(s[1]);
}
