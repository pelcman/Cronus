// エルナス魔法石 (オルビス塔1層, wz script = ossyria3_2) — 塔20層への近道。
// 料金は簡易仕様(創作)。
var COST = 5000;
function start() {
    if (!cm.askYesNo("塔を淡く照らす魔法石だ。力を借りれば#bオルビス塔<20層>#kまで一気に昇れる。"
        + "\r\n手数料は" + COST + "メル。使うかい?")) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようだ。魔法石は沈黙している…。");
        return;
    }
    player.gainMeso(-COST);
    player.warp(200080200);
}
