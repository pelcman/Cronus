// Example portal script (place at CRONUS_SCRIPTS/portal/<scriptName>.js).
// The file name must match a portal's wz `script` field (pn's `script` value).
// Portal scripts have NO dialog — they run once when the player steps on the portal.
// Only the `player` global is available:
//   player.getName/getLevel/getMapId/getMeso/getJob/getHp/getMaxHp/...
//   player.warp(mapId[, portal])  <- the usual thing a portal does
//   player.gainMeso(n) / heal() / setJob(job) / ...
//
// This example gates a portal on level: only level 30+ may pass to the dungeon.
function start() {
    if (player.getLevel() >= 30) {
        player.warp(200000000); // dungeon map id — adjust to your wz
    }
    // else: do nothing — the player just doesn't go through.
}
