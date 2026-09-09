// 司令室 221000300 → 航海室 120000101 (earth01): ワープカード(4031890)を持っているときだけ使える。出典 Reference/Cosmic/scripts/portal/enter_earth01.js。
function start() {
    if (!player.haveItem(4031890)) {
        player.message("このポータルを使うには #t4031890# が必要だ。");
        return;
    }
    player.warpPortal(120000101, "earth01");
}
