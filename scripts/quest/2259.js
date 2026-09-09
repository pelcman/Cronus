// 夜の話はスコルピオンが聞く (quest 2259, NPC 2110005 ラクダ) — マガティア側の 260020700 で落ち合う。
// 出典 Reference/Cosmic/scripts/quest/2259.js。JMS: 開始 q2259s / 終了 q2259e。台詞は創作。
function start() {
    qm.sendNext("よし、話の続きは #b#m260020700##k で。ここから #r東#k へ進んで #rマガティア#k に着いたらそこにいる。さあ行った。");
    player.startQuest(2259);
}
function end() {
    if (player.getMapId() == 260020000) {
        qm.sendNext("おや、まだここにいたのか？　#b#m260020700##k へは、ここから #r東#k へ進んで #rマガティア#k まで行くんだ。さあ。");
        return;
    }
    qm.sendNext("おお、来たな。ここなら近くにミーアキャットもいない、盗み聞きの心配はなさそうだ。よし、お前なら #rキノコ城#k へ行けるだろう。#bレベル30#k になったらまた話しかけてくれ。");
    player.completeQuest(2259);
}
