// ナナ (ルディブリアム, wz script = florina2) — フロリナビーチ(110000000)への送迎。行き先はビーチのみ。
// 台詞は JMS 原文(Riremito/jms_scripts より)。2000メル、またはフリーパスカード(4031134)所持で無料
// (カードは消費しない: アイテム説明「持っていると…ただで行くことができる」に従う)。帰路(パイソン)のため乗船元を記憶する。
var PASS = 4031134;
var PRICE = 2000;
var BEACH = 110000000;
function start() {
    var pick = cm.askMenu("港口から少し離れたところに#bビーチ#kという幻想的な海岸があると聞いたことはあるか？#b2000メル#kを払うか#bフリーパスカード#kを持っているなら、いつでも俺がそこに運んでやるぜ。"
        + "\r\n#L0##b2000メル#k払う#l"
        + "\r\n#L1##bフリーパスカード#kを持っている。#l"
        + "\r\n#L2##bフリーパスカード#kとは？#l");
    if (pick == 0) {
        if (!cm.askYesNo("#b2000メル#kを払ってビーチに行くんだな？でも、そこにもモンスターがいるらしいから油断するよ！では、早速出向準備をするが…今すぐビーチに行くのか？")) return;
        if (player.getMeso() < PRICE) {
            cm.sendOk("メルが足りないようだな。#b2000メル#k用意してから来てくれ。");
            return;
        }
        player.gainMeso(-PRICE);
        sail();
    } else if (pick == 1) {
        if (!player.haveItem(PASS)) {
            cm.sendOk("#bフリーパスカード#kを持っていないようだが？");
            return;
        }
        if (!cm.askYesNo("#bフリーパスカード#kを持っているんだな。では、早速出向準備をするが…今すぐビーチに行くのか？")) return;
        sail();
    } else if (pick == 2) {
        cm.sendOk("#bフリーパスカード#kはフロリナビーチに行くのに必要なカードだ。持っていれば、旅行ガイドを通じてただでフロリナビーチに行ける。");
    }
}
function sail() {
    player.rememberMap();
    player.warp(BEACH);
}
