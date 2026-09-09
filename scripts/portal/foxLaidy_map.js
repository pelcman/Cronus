// 狐の丘 222010300: 魂取り戻し(3647)の進行中にキツネのしっぽ(4031793)を持っていれば 冷たく寒い森 922220000 (east00) へ、そうでなければ 虎の丘 222010200 (east00)。
// Cosmic が立てる GMS 専用の追跡クエスト 23647 は JMS に無いので省く。出典 Reference/Cosmic/scripts/portal/foxLaidy_map.js。
function start() {
    if (player.hasQuest(3647) && player.haveItem(4031793)) {
        player.warpPortal(922220000, "east00");
        return;
    }
    player.warpPortal(222010200, "east00");
}
