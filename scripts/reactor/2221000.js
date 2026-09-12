// 山奥の廃屋 222010401 の祭壇 2221000: 壊すと 黄鬼(7130400) が現れる。出典 Reference/Cosmic/scripts/reactor/2221000.js。
function start() {
    player.spawnMob(7130400, 1);
    player.message("黄鬼 が現れた！");
}
