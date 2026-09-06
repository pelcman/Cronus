// 改札口 (地下鉄 切符売り場, wz script = subway_in) — 1号線・カニングスクエア駅・3号線工事場(切符制)への改札。
// 選択肢と行き先は JMS 原文/Riremitoさん版準拠。工事場は入場券(4031036/37/38)と引き換えで B1/B2/B3 へ。行き先はこの5マップに限定。
var SITES = [
    ["工事場B1", 4031036, 103000900],
    ["工事場B2", 4031037, 103000903],
    ["工事場B3", 4031038, 103000906]
];
function start() {
    var pick = cm.askMenu("行先を選択してください。"
        + "\r\n#L0##b#e1号線#n#k#l"
        + "\r\n#L1##bカニングスクエア<地下鉄搭乗>#k#l"
        + "\r\n#L2##b工事場#k#l");
    if (pick == 0) {
        player.warpPortal(103000101, "out00");
        return;
    }
    if (pick == 1) {
        player.warpPortal(103000310, "out00");
        return;
    }
    if (pick != 2) return;
    var menu = "どの工事場に入りますか？入場券が必要です。";
    var any = false;
    for (var i = 0; i < SITES.length; i++) {
        if (player.haveItem(SITES[i][1])) {
            menu += "\r\n#L" + i + "##b" + SITES[i][0] + "#k#l";
            any = true;
        }
    }
    if (!any) {
        cm.sendOk("工事場の入場券をお持ちではないようです。切符は隣の#bウンイ#kが販売しています。");
        return;
    }
    var site = cm.askMenu(menu);
    if (site < 0 || site >= SITES.length || !player.haveItem(SITES[site][1])) return;
    player.gainItem(SITES[site][1], -1);
    player.warpPortal(SITES[site][2], "st00");
}
