// 不思議な装置 — パプラタス召喚 (簡略版)
// 8500001 を倒すと第2形態 8500002 が現れる(wz の revive 連鎖)。
function start() {
    if (player.mobCount() > 0) {
        cm.sendOk("装置は沈黙している……時計塔の主との戦いが終わっていないようだ。");
        return;
    }
    if (cm.askYesNo("装置に触れると、時計塔の最深部からパプラタスが現れる。起動するか?")) {
        player.spawnMob(8500001, 1);
        cm.sendOk("チクタク、チクタク……時計の音が大きくなっていく!");
    }
}
