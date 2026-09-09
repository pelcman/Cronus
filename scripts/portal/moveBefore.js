// 鍛冶屋外部 108000710 (east00) → 大将翁の鍛冶屋 108000700 (west00): アランの鏡の洞窟の戻り。出典 Reference/Cosmic/scripts/portal/moveBefore.js(mapId - 10)。
// 本物の鍛冶屋外部以外では何もしない。
function start() {
    if (player.getMapId() != 108000710) {
        return;
    }
    player.warpPortal(108000700, "west00");
}
