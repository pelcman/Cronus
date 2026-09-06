// アセソン (アリアント乗降場, wz script = get_ticket) — アリアント→オルビス(ステーション 200000151)の乗船係。行き先はここのみ。
// 台詞は Riremitoさん版。チケット(4031045)と引き換えに目的地へ直行(実飛行化はフェーズ3の残件)。
var TICKET = 4031045;
var DEST = 200000151;
function start() {
    if (!player.haveItem(TICKET)) {
        cm.sendOk("乗船には#b#t" + TICKET + "##kが必要です。チケットは隣の#bシラス#kが販売しています。");
        return;
    }
    if (!cm.askYesNo("いったん船に乗ると長旅になりますので急な用があれば先に解決してください。いかがですか？船に乗りますか？")) return;
    player.gainItem(TICKET, -1);
    player.warp(DEST);
}
