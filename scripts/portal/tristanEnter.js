// 神殿の底 105100100 → トリスタンの魂 105100101 (in00): メモの持ち主は？(2238) を終えた者だけが入れる。出典 Reference/Cosmic/scripts/portal/tristanEnter.js。
function start() {
    if (!player.isQuestDone(2238)) {
        player.message("不思議な力に阻まれて、中に入れない。");
        return;
    }
    player.warpPortal(105100101, "in00");
}
