// ケロベン (生命の洞窟入口, wz script = hontale_keroben) — 洞窟の入口までの案内。
function start() {
    if (cm.askYesNo("ケロケロ…この先の#b洞窟の入口#kまで案内してやろうか?"
        + "\r\nホーンテイルの巣に続く道だ。心の準備はいいか?")) {
        player.warp(240050000);
    } else {
        cm.sendOk("ケロ。賢明な判断かもしれんな。");
    }
}
