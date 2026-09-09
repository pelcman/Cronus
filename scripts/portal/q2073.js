// 東の森 100030000 → ユータの豚農場 900000000: カミラのガラス玉(2073)の進行中だけ入れる私有地。出典 Reference/Cosmic/scripts/portal/q2073.js。
function start() {
    if (!player.hasQuest(2073)) {
        player.message("私有地だ。用事のあるときしか入れない。");
        return;
    }
    player.warp(900000000, 0);
}
