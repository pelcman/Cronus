// リニ (オルビス ステーション<エリニア行き>, wz script = get_ticket) — エリニア行き飛行船の乗船係。
// 台詞は Riremitoさん版。乗船受付中(15分周期の最初の10分)はチケット(4031047)と引き換えに控え室(200000112)へ。
// 出発はサーバーのスケジューラ(Airship.cs)が控え室の全員を機内へ移す。
var TICKET = 4031047;
var WAITING_ROOM = 200000112;
function start() {
    if (!player.airshipBoarding()) {
        cm.sendOk("ただいま船は航海中です。次の便は#b約5分後#kに乗船受付を始めますので、少々お待ちください。");
        return;
    }
    if (!player.haveItem(TICKET)) {
        cm.sendOk("乗船には#b#t" + TICKET + "##kが必要です。チケット売場の#bイフ#kから買えますよ。");
        return;
    }
    if (!cm.askYesNo("いったん船に乗ると長旅になりますので急な用があれば先に解決してください。いかがですか？船に乗りますか？"
        + "\r\n(出発まであと約#b" + player.airshipMinutes() + "分#k)")) return;
    player.gainItem(TICKET, -1);
    player.warp(WAITING_ROOM);
}
