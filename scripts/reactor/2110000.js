// 知られざる閉鉱 (280010011 ほか 15 マップ) の罠 2110000: 壊すと出発点 280010000 へ戻される。出典 Reference/Cosmic/scripts/reactor/2110000.js。
function start() {
    player.message("正体不明の力に、出発点へ押し戻された。");
    player.warp(280010000, 0);
}
