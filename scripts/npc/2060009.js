// イルカ (アクアリウム / 白草村 海側波止場, wz script = aqua_taxi) — イルカタクシー。
// 台詞は JMS 原文(Riremito/jms_scripts に残る原文): 四角地帯(230030200)はイルカタクシーチケット(4031242)と引き換え、
// 白草村 海側波止場(251000100)は10,000メル。白草村→アクアリウムの料金は原文が無いため同額(創作)。
// 霧の海(座礁した幽霊船)行きはクエスト連動のため未実装。到着ポータルは Riremitoさん版どおり。
var TICKET = 4031242;
var GREETING = "世の中の全ての海は繋がっています。歩くと遠い所でも海ならすぐですよ。どうですか？#bイルカタクシー#kに乗って移動しますか？\r\n\r\n[DEV] 霧の海（座礁した幽霊船）行きは未実装です。";
function start() {
    if (player.getMapId() == 251000100) {
        if (!cm.askYesNo(GREETING + "\r\n#b10000メル#kを払い、#m230000000#に移動しますか？")) return;
        if (!pay(10000)) return;
        player.warpPortal(230000000, "market00");
        return;
    }
    var pick = cm.askMenu(GREETING
        + "\r\n#L0##bイルカタクシーチケット#kを使用して#m230030200#に移動する#l"
        + "\r\n#L1##b10000メル#kを払い、#m251000100#に移動する。#l");
    if (pick == 0) {
        if (!player.haveItem(TICKET)) {
            cm.sendOk("#b#t" + TICKET + "##kをお持ちでないようです。");
            return;
        }
        player.gainItem(TICKET, -1);
        player.warpPortal(230030200, "east00");
    } else if (pick == 1) {
        if (!pay(10000)) return;
        player.warpPortal(251000100, "out00");
    }
}
function pay(cost) {
    if (player.getMeso() < cost) {
        cm.sendOk("メルが足りないようです。料金は" + cost + "メルです。");
        return false;
    }
    player.gainMeso(-cost);
    return true;
}
