// ゼラス (オルビス ステーション<アリアント行き>, wz script = get_ticket) — オルビス→アリアント(乗降場 260000100)の乗船係。行き先はここのみ。
// 台詞は Riremitoさん版。チケット(4031576)と引き換えに目的地へ直行(実飛行化はフェーズ3の残件)。
var TICKET = 4031576;
var DEST = 260000100;
function start() {
    if (!player.haveItem(TICKET)) {
        cm.sendOk("乗船には#b#t" + TICKET + "##kが必要です。チケットはチケット売場の#bイフ#kが販売しています。");
        return;
    }
    if (!cm.askYesNo("いったん船に乗ると長旅になりますので急な用があれば先に解決してください。いかがですか？船に乗りますか？")) return;
    player.gainItem(TICKET, -1);
    player.warp(DEST);
}
