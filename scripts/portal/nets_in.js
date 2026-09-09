// サヘル地帯3 260020500 → ピラミッドの丘 926010000 (ポータル 4): ネトのピラミッド入口。戻り先を覚えておき nets_out で戻す。
// 出典 Reference/Cosmic/scripts/portal/nets_in.js (saveLocation "MIRROR" → rememberMap)。
function start() {
    player.rememberMap();
    player.warp(926010000, 4);
}
