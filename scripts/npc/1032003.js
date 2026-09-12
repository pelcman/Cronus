// シェイン 1032003 (エリニア 101000000, JMS スクリプト名 herb_in) — 5,000 メソで 忍耐の森(101000100 / 101000102)へ入れる。Lv25 以上。
// サビトラマの薬(2050 / 2051)の進行中はその段へ、それ以外は Lv25〜49 が 1段階、Lv50 以上が 3段階。出典 Reference/Cosmic/scripts/npc/1032003.js。台詞は創作。
function start() {
    if (player.getLevel() < 25) {
        cm.sendOk("忍耐の森に入るには、もう少しレベルが必要だよ。");
        return;
    }
    if (!cm.askYesNo("やあ、シェインだ。少しの料金で忍耐の森に入れてあげよう。#b5,000#k メソで入るかい？")) {
        cm.sendOk("そうか。また今度ね。");
        return;
    }
    if (player.getMeso() < 5000) {
        cm.sendOk("悪いけど、メソが足りないみたいだね！");
        return;
    }
    var map = player.getLevel() >= 50 ? 101000102 : 101000100;
    if (player.hasQuest(2050)) {
        map = 101000100;
    } else if (player.hasQuest(2051)) {
        map = 101000102;
    }
    player.gainMeso(-5000);
    player.warp(map, 0);
}
