// 正騎士 (quest 29907, NPC 1101000 シグナス) — Lv30 以上のシグナス騎士団(2次職以上) に自動で出る称号クエスト。受注(自動開始)の場で 正騎士の勲章(1142067) を
// 受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29907.js。JMS: 開始スクリプト q29907s のみ(完了側の宣言なし)、
// Check は職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142067)) {
        qm.sendOk("#b#t1142067##k は、すでにそなたの手にある。");
        return;
    }
    qm.sendNext("よくぞここまで励んだ。そなたに #b<正騎士>#k の称号と、#b#t1142067##k を授けよう。");
    player.gainItem(1142067, 1);
    player.startQuest(29907);
    player.completeQuest(29907);
}
