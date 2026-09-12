// オモチャ工場-1工程 (220020000〜, 13 マップ) の仕掛け 2202002: 機械工ルキの整備(3238) の進行中なら 秘密工程１ 922000020、それ以外は 秘密通路 922000009 へ。
// 出典 Reference/Cosmic/scripts/reactor/2202002.js。
function start() {
    player.warp(player.hasQuest(3238) ? 922000020 : 922000009, 0);
}
