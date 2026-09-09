// 見習い騎士の終わり (quest 20200, NPC 1101002 ナインハート) — Lv30 になった見習い騎士(1次職)に騎士クラス試験を案内し、その場で受注→完了。
// 出典 Reference/Cosmic/scripts/quest/20200.js。JMS: 開始スクリプト q20200s、normalAutoStart、Check は Lv30・職業 1100〜1500、Act は空。台詞は創作。
function start() {
    if (!qm.askAccept("#h0#？　おお、前に会ったときからずいぶんレベルが上がったな。もう見習い騎士の任務は十分こなしたようだ。正騎士になる気はあるか？")) {
        qm.sendOk("ふむ…まだ見習いとして片付けたい任務が残っているのか？　気が向いたら、また声をかけなさい。");
        return;
    }
    player.startQuest(20200);
    player.completeQuest(20200);
    qm.sendOk("騎士クラス試験を受けるなら、#bエレヴ#k に来なさい。各騎士団の団長が、それぞれの試験を用意している。");
}
