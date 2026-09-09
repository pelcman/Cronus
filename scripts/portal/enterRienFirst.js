// リエン西のフィールド 140010000 → リエン村 140000000: リリンの話(21014)前のレジェンド(2000)は入口側 st00 に、それ以外は west00 に出る。
// 出典 Reference/Cosmic/scripts/portal/enterRienFirst.js。
function start() {
    if (player.getJob() == 2000 && !player.isQuestDone(21014)) {
        player.warpPortal(140000000, "st00");
        return;
    }
    player.warpPortal(140000000, "west00");
}
