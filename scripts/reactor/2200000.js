// 人形の家 922000010 の罠 2200000: 壊すと エオス塔74階 221023200 へ戻される。出典 Reference/Cosmic/scripts/reactor/2200000.js。
function start() {
    player.message("引っかかった！　次はうまくやろう！");
    player.warp(221023200, 0);
}
