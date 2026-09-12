// 古代氷石 2030014 (氷の谷 921100100, JMS スクリプト名 s4freeze_item) — オリハルコンハンマー(4031450) で砕くと 古代の氷粉(2280011) が手に入る(4次転職の試練)。
// 出典 Reference/Cosmic/scripts/npc/2030014.js。台詞は創作。
function start() {
    if (!player.haveItem(4031450)) {
        cm.sendOk("（太古の氷が固く凍りついている。並のハンマーでは砕けそうにない。）");
        return;
    }
    player.gainItem(4031450, -1);
    player.gainItem(2280011, 1);
    cm.sendOk("（オリハルコンハンマーで氷を砕くと、#b#t2280011##k が手に入った。ハンマーは砕けてしまった。）");
}
