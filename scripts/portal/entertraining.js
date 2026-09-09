// 冒険者の修練場入口 1010000 → 冒険者の修練場1〜4 (1010100〜1010400, ポータル 4): マイの修行 1041〜1044 の進行中の段に応じて入る。
// 出典 Reference/Cosmic/scripts/portal/entertraining.js。
function start() {
    if (player.hasQuest(1041)) { player.warp(1010100, 4); return; }
    if (player.hasQuest(1042)) { player.warp(1010200, 4); return; }
    if (player.hasQuest(1043)) { player.warp(1010300, 4); return; }
    if (player.hasQuest(1044)) { player.warp(1010400, 4); return; }
    player.message("マイの修行を受けている冒険者だけが入れる。");
}
