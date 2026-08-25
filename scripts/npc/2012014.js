// オルビス魔法石 (オルビス塔20層, wz script = ossyria3_1) — 塔1層への近道。
// 料金は簡易仕様(創作)。
var COST = 5000;
function start() {
    if (!cm.askYesNo("塔を淡く照らす魔法石だ。力を借りれば#bオルビス塔<1層>#kまで一気に降りられる。"
        + "\r\n手数料は" + COST + "メル。使うかい?")) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようだ。魔法石は沈黙している…。");
        return;
    }
    player.gainMeso(-COST);
    player.warp(200082100);
}
