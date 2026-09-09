// 神獣の涙(ナイトウォーカー) (quest 20314, NPC 1101006 イカルト) — 変化師(20304)を終えた Lv70 の正騎士(1410)の 3次転職(1411)。
// 2次のスキルに SP を使い切っていないと断られる(余り SP は (Lv-70)*3 まで)。勲章 上級騎士の勲章(1142068) は JMS では 29908 が渡す。
// 出典 Reference/Cosmic/scripts/quest/20314.js。JMS: 開始スクリプト q20314s(完了側の宣言なし → その場で受注→完了)、Check は 20304 完了・Lv70、Act は空。台詞は創作。
function start() {
    qm.sendNext("変化師から取り戻した宝石は、シンスの涙…神獣の涙だ。女王陛下が、そなたの働きをたいそう喜んでおられる。");
    if (!qm.askYesNo("大きな災いを未然に防いだ功績をたたえ、陛下はそなたに新たな称号を授けると仰せだ。#r上級騎士#k として、ナイトウォーカーの真の力を受け取る準備はいいか？")) {
        qm.sendOk("準備ができたら、また来なさい。");
        return;
    }
    if (player.getJob() != 1410) {
        qm.sendOk("そなたはもう上級騎士ではないか。");
        return;
    }
    if (player.getSp() > (player.getLevel() - 70) * 3) {
        qm.sendOk("#bSP#k がまだ余りすぎている。2次のスキルにもっと SP を使ってから来なさい。上級騎士の称号はそれからだ。");
        return;
    }
    player.changeJob(1411);
    player.startQuest(20314);
    player.completeQuest(20314);
    qm.sendOk("#h0#、今この瞬間から、そなたは #b上級騎士#k だ。新たなスキルと SP を確かめ、さらなる高みを目指しなさい。");
}
