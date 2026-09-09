// 目覚めたアラン (quest 29924, NPC 9000066 ダリア — JMS v186 ではどのマップにも置かれておらず、自動開始の会話用) — Lv10 以上のアラン(1次職以上) に自動で出る
// 称号クエスト。受注(自動開始)の場で 目覚めたアランの勲章(1142129) を受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29924.js。
// JMS: 開始スクリプト q29924s のみ、Check は勲章未所持・職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142129)) {
        qm.sendOk("#b#t1142129##k はもう持っているみたいね。");
        return;
    }
    qm.sendNext("#b<目覚めたアラン>#k の称号を手に入れた。英雄の歩みの証、#b#t1142129##k を受け取って。");
    player.gainItem(1142129, 1);
    player.startQuest(29924);
    player.completeQuest(29924);
}
