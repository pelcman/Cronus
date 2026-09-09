// とても小さな一歩 (quest 21704, NPC 1201000 リリン) — 修練所での成果を報告。EXP 500 は Cosmic の値(JMS の Act は空)。
// 出典 Reference/Cosmic/scripts/quest/21704.js。JMS: 開始スクリプト q21704s(完了側は宣言なし → その場で受注→完了)。台詞は創作。
function start() {
    qm.sendNext("修行はどうでしたか？　教官の #p1202006# は、あなたのことをとても褒めていましたよ。");
    qm.sendNext("（コンボアビリティを思い出せたと伝える。）");
    qm.sendNext("素晴らしい！　正直に言えば、教官の指導というより、あなた自身の力だと思いますけどね。");
    player.startQuest(21704);
    player.completeQuest(21704);
    player.gainExp(500);
}
