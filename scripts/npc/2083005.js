// 生命の泉 2083005 (ホーンテイルの洞窟入口 240050400, JMS スクリプト名 s4holycharge) — 聖なる騎士の剣(6280)の進行中に 聖杯(4031454) で泉の水をくむと
// 生命の聖水(4031455) になる。出典 Reference/Cosmic/scripts/npc/2083005.js。台詞は創作。
function start() {
    if (!(player.hasQuest(6280) && player.haveItem(4031454))) {
        cm.sendOk("（澄んだ泉が湧いている。）");
        return;
    }
    cm.sendOk("（泉の水を聖杯にくんだ。）");
    player.gainItem(4031454, -1);
    player.gainItem(4031455, 1);
}
