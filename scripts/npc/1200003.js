// プロ (リエン ペンギン港, wz script = contimoveRieRit) — リエン→リス港(104000000)の船。行き先はここのみ。
// 台詞は簡易(創作: Riremitoさん版も「テキスト適当」、料金は往路と同額)。運航は直行ワープ(実飛行化はフェーズ3の残件)。料金800メル。
var DEST = 104000000;
var FARE = 800;
function start() {
    if (!cm.askYesNo("ビクトリアアイランドの#b港口#kに戻るのか？#b料金800#kメルで乗せていってやる。行くのにかかる時間は、訳1分だ。")) return;
    if (player.getMeso() < FARE) {
        cm.sendOk("メルが足りないようだな。#b800メル#k用意してから来てくれ。");
        return;
    }
    player.gainMeso(-FARE);
    player.warp(DEST);
}
