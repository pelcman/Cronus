// 大将翁の鍛冶屋 108000700 (west00) → 鍛冶屋外部 108000710 (east00): アランの鏡の洞窟。JMS では 108000700 と 108000710 だけが本物で、
// 続く 108000701〜/108000711〜 はモブもポータルも無い複製枠(サーバーが複製して使う想定)。出典 Reference/Cosmic/scripts/portal/moveNext.js(mapId + 10)。
// 本物の鍛冶屋以外(複製枠など)では何もしない。
function start() {
    if (player.getMapId() != 108000700) {
        return;
    }
    player.warpPortal(108000710, "east00");
}
