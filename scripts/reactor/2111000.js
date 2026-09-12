// 知られざる閉鉱 (280010031 ほか 14 マップ) の宝箱 2111000: 開けると ジュニアミミック(9300004) が 3 体飛び出す。出典 Reference/Cosmic/scripts/reactor/2111000.js。
function start() {
    player.message("わっ！　箱の中にモンスターが！");
    player.spawnMob(9300004, 3);
}
