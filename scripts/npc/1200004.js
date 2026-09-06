// プロ (リス港, wz script = contimoveRitRie) — リス港→リエン(ペンギン港 140020300)の船。行き先はここのみ。
// 台詞は JMS 原文(BB後、Riremito/jms_scripts より)。運航は直行ワープ(実飛行化はフェーズ3の残件)。料金800メル。
var DEST = 140020300;
var FARE = 800;
function start() {
    if (!cm.askYesNo("もしやビクトリアアイランドを離れ、我々の村に行くつもりか？この船に乗ると#bリエン#kまで乗せていってやれるが…#b料金800#kメル必要だ。リエンに行くかい？行くのにかかる時間は、訳1分だ。")) return;
    if (player.getMeso() < FARE) {
        cm.sendOk("メルが足りないようだな。#b800メル#k用意してから来てくれ。");
        return;
    }
    player.gainMeso(-FARE);
    player.warp(DEST);
}
