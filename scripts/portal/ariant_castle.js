// アリアント宮殿 260000300 → アリアント宮殿<庭園> 260000301 (ポータル 5): 王宮出入資格証(4031582)を持つ者だけ。出典 Reference/Cosmic/scripts/portal/ariant_castle.js。
function start() {
    if (!player.haveItem(4031582)) {
        player.message("宮殿に入るには #t4031582# が必要だ。");
        return;
    }
    player.warp(260000301, 5);
}
