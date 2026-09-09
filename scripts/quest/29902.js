// ベテラン冒険家 (quest 29902, NPC 9000040 ダリア: ヘネシス/エリニア/ペリオン/カニング/リス港/スリーピーウッド/ノーチラス) — Lv70 以上の 3次職以上の冒険家 に
// 自動で出る称号クエスト。受注(自動開始)で称号獲得の知らせ、ダリアに話すと ベテラン冒険家の勲章(1142109) を受け取って完了。
// 出典 Reference/Cosmic/scripts/quest/29902.js。JMS: q29902s / q29902e、Check は勲章未所持・職業・Lv(クライアント側で判定)、
// Act は両側とも空なので勲章の付与はスクリプト側。台詞は創作。
function start() {
    qm.sendOk("#b<ベテラン冒険家>#k の称号を手に入れた。#b#p9000040##k に話しかけると、称号の勲章を受け取ることができる。");
    player.startQuest(29902);
}

function end() {
    if (player.haveItem(1142109)) {
        qm.sendOk("あら、#b#t1142109##k はもう持っているみたいね。");
        return;
    }
    qm.sendNext("#b<ベテラン冒険家>#k の称号獲得、おめでとう！　あなたの冒険の証、#b#t1142109##k を受け取って。");
    player.gainItem(1142109, 1);
    player.completeQuest(29902);
}
