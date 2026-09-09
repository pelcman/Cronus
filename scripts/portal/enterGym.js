// リエン修行場入口 140010100 → リエン修行場1〜3 (914010000 / 914010100 / 914010200, ポータル 1): 修行だけが唯一の道！1〜3 (21701〜21703) の
// 進行中の段に応じて入る。それ以外は入れない。出典 Reference/Cosmic/scripts/portal/enterGym.js。
function start() {
    if (player.hasQuest(21701)) { player.warp(914010000, 1); return; }
    if (player.hasQuest(21702)) { player.warp(914010100, 1); return; }
    if (player.hasQuest(21703)) { player.warp(914010200, 1); return; }
    player.message("ペンギンの修練場には、教官プオの修行を受けている者だけが入れる。");
}
