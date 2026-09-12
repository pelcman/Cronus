// 紅葉玉 2012023 (出会いの丘 200000300, JMS スクリプト名 s4tornado) — ガラス玉(4031476) を 紅葉玉(4031456) に変える(4次転職の試練)。出典 Reference/Cosmic/scripts/npc/2012023.js。台詞は創作。
function start() {
    if (!player.haveItem(4031476)) {
        cm.sendOk("（紅葉の光を宿した玉だ。ガラス玉があれば、その光を移せそうだ。）");
        return;
    }
    player.gainItem(4031476, -1);
    player.gainItem(4031456, 1);
    cm.sendOk("（ガラス玉に紅葉の光が宿り、#b#t4031456##k になった。）");
}
