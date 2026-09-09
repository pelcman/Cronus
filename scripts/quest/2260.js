// キノコ城に向けて！ (quest 2260, NPC 2110005 ラクダ) — 2次転職済みならキノコ城への道を教えて完了。
// 出典 Reference/Cosmic/scripts/quest/2260.js。JMS: 開始 q2260s / 終了 q2260e。2次転職の判定は職業コードの 2桁目(X10/X20 など)。台詞は創作。
function start() {
    qm.sendNext("#b2次転職#k を済ませたら、#bキノコ城#k のことを話してやろう。");
    player.startQuest(2260);
}
function end() {
    var job = player.getJob();
    if (job < 100 || job % 100 == 0) {
        qm.sendNext("おや、まだ #r2次転職#k を済ませていないのか？");
        return;
    }
    qm.sendNext("よし、#bキノコ城#k へ行く準備ができたようだな。#rヘネシス#k の #b西#k にある木の砦を登ってポータルに入り、向こうでも #r西#k へ進めばポータルが待っている。");
    player.completeQuest(2260);
}
