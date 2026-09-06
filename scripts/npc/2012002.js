// エリン (オルビス 控え室<エリニア行き>, wz script = goOutWaitingRoom) — 控え室から昇降場へ戻る(チケットの返金なし)。
// 台詞は OdinMS 系(GMS)の英文を日本語化(創作)。行き先は控え室に対応する昇降場のみ。
var EXITS = [
    [200000112, 200000111]
];
function start() {
    var here = player.getMapId();
    var station = EXITS[0][1];
    for (var i = 0; i < EXITS.length; i++) {
        if (EXITS[i][0] == here) station = EXITS[i][1];
    }
    if (!cm.askYesNo("控え室から出ますか？出ることはできますが、チケットは返金されません。本当にこの部屋から出ますか？")) {
        cm.sendOk("もうすぐ目的地に着きます。他の人と話でもしていれば、あっという間に着きますよ。");
        return;
    }
    player.warp(station);
}
