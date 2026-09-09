// ドランの願い (quest 3353, 受注 NPC 2111006 ファウェン → 完了 2111002 ドラン) — もう一度ドランの研究室 926120200 へ送ってもらう。
// 完了側はデータ経路(EXP 5000)。出典 Reference/Cosmic/scripts/quest/3353.js。JMS: 開始スクリプト q3353s、Check は 3346 完了と Lv75。台詞は創作。
function start() {
    qm.sendNext("なるほど。ドランはヒューマノイドの暴走を止めたいが、協会は彼を受け入れない…板挟みというわけか。");
    if (!qm.askAccept("それなら、もう一度あそこへ行って、ドラン本人から詳しい話を聞いてきてくれないか？")) {
        qm.sendOk("そうか。行く気になったら、また声をかけてくれ。");
        return;
    }
    player.startQuest(3353);
    player.warp(926120200, 0);
}
