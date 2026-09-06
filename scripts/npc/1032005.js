// エリニア高級タクシー (エリニア, wz script = mTaxi) — アリの巣広場(105070001)行きの高級タクシー。行き先はここのみ。
// 台詞と料金(10,000メル)は JMS 原文(Riremito/jms_scripts より)。初心者(職業0)の割引額1,000メルは GMS 準拠の創作。
var DEST = 105070001;
function start() {
    var beginner = player.getJob() == 0;
    var cost = beginner ? 1000 : 10000;
    var shown = beginner ? "1,000" : "10,000";
    if (!cm.askYesNo("初心者ではない方には決められた料金が請求されます。アリの巣広場はビクトリアアイランドの中央にあるダンジョンの奥の、24時間屋台が棲んでいるところです。#b" + shown + "メル#kでアリの巣広場まで如何でしょうか？")) return;
    if (player.getMeso() < cost) {
        cm.sendOk("メルが足りないようです。料金は" + shown + "メルです。");
        return;
    }
    player.gainMeso(-cost);
    player.warp(DEST);
}
