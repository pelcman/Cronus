// 騎士団長 (quest 29909, NPC 1101000 シグナス) — Lv120 以上のシグナス騎士団(4次職) に自動で出る称号クエスト。受注(自動開始)の場で 騎士団長の勲章(1142069) を
// 受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29909.js。JMS: 開始スクリプト q29909s のみ(完了側の宣言なし)、
// Check は職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142069)) {
        qm.sendOk("#b#t1142069##k は、すでにそなたの手にある。");
        return;
    }
    qm.sendNext("よくぞここまで励んだ。そなたに #b<騎士団長>#k の称号と、#b#t1142069##k を授けよう。");
    player.gainItem(1142069, 1);
    player.startQuest(29909);
    player.completeQuest(29909);
}
