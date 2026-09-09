// 初心者冒険家 (quest 29900, NPC 9000040 ダリア: ヘネシス/エリニア/ペリオン/カニング/リス港/スリーピーウッド/ノーチラス) — Lv8 以上の冒険家(初心者〜) に
// 自動で出る称号クエスト。受注(自動開始)で称号獲得の知らせ、ダリアに話すと 初心者冒険家の勲章(1142107) を受け取って完了。
// 出典 Reference/Cosmic/scripts/quest/29900.js。JMS: q29900s / q29900e、Check は勲章未所持・職業・Lv(クライアント側で判定)、
// Act は両側とも空なので勲章の付与はスクリプト側。台詞は創作。
function start() {
    qm.sendOk("#b<初心者冒険家>#k の称号を手に入れた。#b#p9000040##k に話しかけると、称号の勲章を受け取ることができる。");
    player.startQuest(29900);
}

function end() {
    if (player.haveItem(1142107)) {
        qm.sendOk("あら、#b#t1142107##k はもう持っているみたいね。");
        return;
    }
    qm.sendNext("#b<初心者冒険家>#k の称号獲得、おめでとう！　あなたの冒険の証、#b#t1142107##k を受け取って。");
    player.gainItem(1142107, 1);
    player.completeQuest(29900);
}
