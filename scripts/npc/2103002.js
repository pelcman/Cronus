// 王妃の飾り棚 2103002 (アリアント宮殿<王室> 260000303, JMS スクリプト名 ariant_ring) — 砂絵団員の頼み!? 3(3923)の進行中に調べると 王妃の指輪(4031578) を
// くすねられる(1 つだけ)。出典 Reference/Cosmic/scripts/npc/2103002.js。台詞は創作。
function start() {
    if (player.hasQuest(3923) && !player.haveItem(4031578)) {
        cm.sendOk("（指輪をくすねた。早くこの場を離れよう！）");
        player.gainItem(4031578, 1);
        return;
    }
    cm.sendOk("（王妃の宝飾品が飾られている。）");
}
