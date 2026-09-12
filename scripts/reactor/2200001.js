// オモチャ工場-メイン工程2 / 2工程 (220030200〜220030400) の仕掛け 2200001: 秘密工程１(922000020) か 秘密工程２(922000021) のどちらかへ飛ばされる。
// 出典 Reference/Cosmic/scripts/reactor/2200001.js。
function start() {
    player.message("秘密の工程を見つけた！");
    player.warp(Math.random() < 0.5 ? 922000020 : 922000021, 0);
}
