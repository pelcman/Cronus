// アルカドノの本棚 2111010 (光が消えた研究室 926120000, JMS スクリプト名 magatia_dark1) — 事件、そして失踪した錬金術師(3309)の進行中に調べると
// 秘密文書(4031708) が見つかる(一度だけ)。出典 Reference/Cosmic/scripts/npc/2111010.js。台詞は創作。
function start() {
    if (player.hasQuest(3309) && !player.haveItem(4031708)) {
        player.gainItem(4031708, 1);
        cm.sendOk("（本棚の奥に、アルカドノの #b#t4031708##k が隠されていた。）");
        return;
    }
    cm.sendOk("（古い研究書が並んでいる。特に変わったものは無い。）");
}
