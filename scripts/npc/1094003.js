// 草原 1094003 (ノーチラス 120000000, JMS スクリプト名 nautil_Abel1) — メガネを探して！(2186)の進行中に調べると、アベルのメガネ(4031853)か誰かのメガネ(4031854 / 4031855)の
// どれかが見つかる(持っていなければ 1 つだけ)。出典 Reference/Cosmic/scripts/npc/1094003.js。台詞は創作。
function start() {
    if (!player.hasQuest(2186)) {
        cm.sendOk("（箱が積んであるだけで、特に何もない…。）");
        return;
    }
    if (player.haveItem(4031853) || player.haveItem(4031854) || player.haveItem(4031855)) {
        cm.sendOk("（ここにあったメガネは #bもう拾った#k。）");
        return;
    }
    cm.sendNext("（箱の隙間に、メガネが落ちている。拾ってみよう。）");
    var roll = Math.floor(Math.random() * 3);
    player.gainItem(roll == 0 ? 4031853 : (roll == 1 ? 4031854 : 4031855), 1);
}
