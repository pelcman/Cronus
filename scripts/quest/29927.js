// 希望の中のアラン (quest 29927, NPC 9000066 ダリア — JMS v186 ではどのマップにも置かれておらず、自動開始の会話用) — Lv120 以上のアラン(4次職) に自動で出る
// 称号クエスト。受注(自動開始)の場で 希望の中のアランの勲章(1142132) を受け取ってそのまま完了する。出典 Reference/Cosmic/scripts/quest/29927.js。
// JMS: 開始スクリプト q29927s のみ、Check は勲章未所持・職業・Lv(クライアント側で判定)、Act は空なので勲章の付与と完了はスクリプト側。台詞は創作。
function start() {
    if (player.haveItem(1142132)) {
        qm.sendOk("#b#t1142132##k はもう持っているみたいね。");
        return;
    }
    qm.sendNext("#b<希望の中のアラン>#k の称号を手に入れた。英雄の歩みの証、#b#t1142132##k を受け取って。");
    player.gainItem(1142132, 1);
    player.startQuest(29927);
    player.completeQuest(29927);
}
