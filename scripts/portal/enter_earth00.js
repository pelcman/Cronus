// 航海室 120000101 → 司令室 221000300 (earth00): ワープカード(4031890)を持っているときだけ使える。出典 Reference/Cosmic/scripts/portal/enter_earth00.js。
function start() {
    if (!player.haveItem(4031890)) {
        player.message("このポータルを使うには #t4031890# が必要だ。");
        return;
    }
    player.warpPortal(221000300, "earth00");
}
