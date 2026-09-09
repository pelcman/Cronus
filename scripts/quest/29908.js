// 上級騎士 (quest 29908, NPC 1101000 シグナス) — Lv70 以上のシグナス騎士団(3次職以上) に自動で出る称号クエスト。受注(自動開始)の場で 上級騎士の勲章(1142068) を
// 受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29908.js。JMS: 開始スクリプト q29908s のみ(完了側の宣言なし)、
// Check は職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142068)) {
        qm.sendOk("#b#t1142068##k は、すでにそなたの手にある。");
        return;
    }
    qm.sendNext("よくぞここまで励んだ。そなたに #b<上級騎士>#k の称号と、#b#t1142068##k を授けよう。");
    player.gainItem(1142068, 1);
    player.startQuest(29908);
    player.completeQuest(29908);
}
