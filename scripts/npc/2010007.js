// ヘラクル — ギルド作成の案内 (作成はギルド本部でギルドウィンドウから)
var GUILD_HQ = 200000301;
var COST = 5000000;
function start() {
    var here = (player.getMapId() == GUILD_HQ);
    var text = "ようこそ、ギルド本部へ。ギルドを作りたいのか?\r\n"
        + "作成料は" + COST + "メル。ギルドウィンドウ(基本キーはG)を開いて「ギルド作成」を選べば、"
        + "この本部でだけ結成の手続きができるぞ。";
    if (here) {
        cm.sendOk(text + "\r\n\r\n準備ができたら、いつでもギルドウィンドウからどうぞ。");
        return;
    }
    if (cm.askYesNo("ギルドの結成にはギルド本部での手続きが必要だ。本部へ案内しようか?")) {
        player.warp(GUILD_HQ);
    } else {
        cm.sendOk("気が変わったらまた声をかけてくれ。");
    }
}
