// マスター冒険家 (quest 29903, NPC 9000040 ダリア: ヘネシス/エリニア/ペリオン/カニング/リス港/スリーピーウッド/ノーチラス) — Lv120 以上の 4次職の冒険家 に
// 自動で出る称号クエスト。受注(自動開始)で称号獲得の知らせ、ダリアに話すと マスター冒険家の勲章(1142110) を受け取って完了。
// 出典 Reference/Cosmic/scripts/quest/29903.js。JMS: q29903s / q29903e、Check は勲章未所持・職業・Lv(クライアント側で判定)、
// Act は両側とも空なので勲章の付与はスクリプト側。台詞は創作。
function start() {
    qm.sendOk("#b<マスター冒険家>#k の称号を手に入れた。#b#p9000040##k に話しかけると、称号の勲章を受け取ることができる。");
    player.startQuest(29903);
}

function end() {
    if (player.haveItem(1142110)) {
        qm.sendOk("あら、#b#t1142110##k はもう持っているみたいね。");
        return;
    }
    qm.sendNext("#b<マスター冒険家>#k の称号獲得、おめでとう！　あなたの冒険の証、#b#t1142110##k を受け取って。");
    player.gainItem(1142110, 1);
    player.completeQuest(29903);
}
