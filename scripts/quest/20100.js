// 選択の岐路 (quest 20100, NPC 1101002 ナインハート) — Lv10 のノーブレスに 5 つの騎士の道を選ぶよう促し、その場で受注→完了する。
// 出典 Reference/Cosmic/scripts/quest/20100.js。JMS: 開始スクリプト q20100s、Check は 20000/20001 完了・Lv10・職業 1000、Act は空。台詞は創作。
function start() {
    if (!qm.askAccept("おお、戻ってきたか。もうレベル 10 になったのだな。いよいよ騎士としての道を選ぶときだ。準備はいいか？")) {
        return;
    }
    player.startQuest(20100);
    player.completeQuest(20100);
    qm.sendOk("では左を見なさい。騎士団の団長たちが待っている。#bソウルマスター#k、#bフレイムウィザード#k、#bウィンドブレイカー#k、#bナイトウォーカー#k、#bストライカー#k…それぞれの団長に話して、自分の道を決めるのだ。");
}
