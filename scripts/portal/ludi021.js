// 秘密通路 922000009 → オモチャ工場-機械室 220020600: 持っている機械部品(4031092)を全部置いて出る。出典 Reference/Cosmic/scripts/portal/ludi021.js。
function start() {
    var n = player.itemQuantity(4031092);
    if (n > 0) {
        player.gainItem(4031092, -n);
    }
    player.warp(220020600, 0);
}
