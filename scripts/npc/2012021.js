// ラミニ (オルビス ステーション<リプレ行き>, wz script = get_ticket) — オルビス→リプレ(ステーション 240000110)の乗船係。行き先はここのみ。
// 台詞は Riremitoさん版。チケット(4031331)と引き換えに目的地へ直行(実飛行化はフェーズ3の残件)。
var TICKET = 4031331;
var DEST = 240000110;
function start() {
    if (!player.haveItem(TICKET)) {
        cm.sendOk("乗船には#b#t" + TICKET + "##kが必要です。チケットはチケット売場の#bイフ#kが販売しています。");
        return;
    }
    if (!cm.askYesNo("いったん船に乗ると長旅になりますので急な用があれば先に解決してください。いかがですか？船に乗りますか？")) return;
    player.gainItem(TICKET, -1);
    player.warp(DEST);
}
