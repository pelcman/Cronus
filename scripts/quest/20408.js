// 女王の騎士団長 (quest 20408, NPC 1101000 シグナス) — 呪いの騒動を収めた騎士(Lv120、X11)を騎士団長(X12)に任命する。勲章 騎士団長の勲章(1142069) は
// JMS では 29909 が渡す(Cosmic はここで渡す)。出典 Reference/Cosmic/scripts/quest/20408.js。JMS: 開始スクリプト q20408s、Check は 20407 完了・Lv120、Act は空。台詞は創作。
function start() {
    qm.sendNext("#h0#…まずは、よくやってくれました。あなたがいなければ、暗黒の魔法使いの呪いは騎士団を蝕んでいたでしょう。");
    qm.sendNext("この一連の出来事で、はっきりしたことが一つあります。暗黒の魔法使いの復活は近い。私たちには、あなたのような騎士が必要です。");
    if (!qm.askAccept("あなたの働きと功績をたたえ…私はあなたを #b騎士団長#k に任命したいと思います。受けてくれますか？")) {
        return;
    }
    var job = player.getJob();
    if (job % 10 == 1) {
        player.changeJob(job + 1);
    }
    player.startQuest(20408);
    player.completeQuest(20408);
    qm.sendOk("#h0#。暗黒の魔法使いに勇敢に立ち向かった功績により、あなたを #b騎士団長#k に任命します。これからも、メイプルワールドのために。");
}
