// timeQuest — タイムロード「思い出の道 / 後悔の道 / 忘却の道」(270010100〜270040000) の関門ポータル。
// 各「道」マップの奥にあり、対応するクエストを完了していれば次の「憩いの広場」または次のゾーンへ進み、
// まだなら安全なゾーン入口へ戻す。出典 Reference/Cosmic/scripts/portal/timeQuest.js を JMS v186 に移植。
// Cosmic の計算式(map=(mapid-270010000)/100、前進は mapid+10、関門クエスト 3501〜3522)は JMS v186 の
// マップ ID・ポータル配線・Check.img のクエストと 1:1 で一致することを確認済み(DevTools で全 16 マップ照合)。
function start() {
    var mapid = player.getMapId();
    if (mapid < 270010100 || mapid > 270040000) {
        return; // this portal only exists on the Road of Time lane maps (270010100〜270040000)
    }
    var map = Math.floor((mapid - 270010000) / 100);

    if (map < 5 && player.isQuestDone(3500 + map)) {
        player.warpPortal(mapid + 10, "out00");
    } else if (map == 5 && player.isQuestDone(3507)) {
        player.warpPortal(270020000, "out00");
    } else if (map > 100 && map < 105 && player.isQuestDone(3407 + map)) {
        player.warpPortal(mapid + 10, "out00");
    } else if (map == 105 && player.isQuestDone(3514)) {
        player.warpPortal(270030000, "out00");
    } else if (map > 200 && map < 205 && player.isQuestDone(3314 + map)) {
        player.warpPortal(mapid + 10, "out00");
    } else if (map == 205 && player.isQuestDone(3519)) {
        player.warpPortal(270040000, "out00");
    } else if (map == 300 && (player.haveItem(4032002) || player.isQuestDone(3522))) {
        player.warpPortal(270040100, "out00");
    } else {
        // Not cleared yet: the oracle says "time flows oddly" and drops the player on a safe lane.
        if (map > 200) {
            player.warpPortal(270030000, "in00");
        } else if (map > 100) {
            player.warpPortal(270020000, "in00");
        } else {
            player.warpPortal(270010000, "in00");
        }
    }
}
