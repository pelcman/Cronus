// 見習い騎士 (quest 29906, NPC 1101000 シグナス) — Lv10 以上のシグナス騎士団(1次職以上) に自動で出る称号クエスト。受注(自動開始)の場で 見習い騎士の勲章(1142066) を
// 受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29906.js。JMS: 開始スクリプト q29906s のみ(完了側の宣言なし)、
// Check は職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142066)) {
        qm.sendOk("#b#t1142066##k は、すでにそなたの手にある。");
        return;
    }
    qm.sendNext("よくぞここまで励んだ。そなたに #b<見習い騎士>#k の称号と、#b#t1142066##k を授けよう。");
    player.gainItem(1142066, 1);
    player.startQuest(29906);
    player.completeQuest(29906);
}
