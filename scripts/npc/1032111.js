// 小さい切り株 1032111 (渓流<バンジージャンプ台> 101010103, JMS スクリプト名 giveSap) — アルウェンが怪しいです！(20716)の進行中に 清い木の樹液(4032142) を
// 汲める(1 つだけ)。出典 Reference/Cosmic/scripts/npc/1032111.js。台詞は創作。
function start() {
    if (player.hasQuest(20716) && !player.haveItem(4032142)) {
        player.gainItem(4032142, 1);
        cm.sendOk("（澄んだ木の樹液を瓶に詰めた。 #i4032142#）");
        return;
    }
    cm.sendOk("（小さな切り株から、樹液が絶えず流れ出している。）");
}
