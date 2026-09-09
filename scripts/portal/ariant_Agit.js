// アリアント集落地 260000200 → 古い空き家 260000201 (ポータル 1): 砂絵団の 弓を持つ男(3928)・鉄槌を持つ男(3931)・短刀を持つ女(3934) を終えた者だけ。
// 出典 Reference/Cosmic/scripts/portal/ariant_Agit.js。
function start() {
    if (!(player.isQuestDone(3928) && player.isQuestDone(3931) && player.isQuestDone(3934))) {
        player.message("ここは砂絵団の仲間しか入れない。");
        return;
    }
    player.warp(260000201, 1);
}
