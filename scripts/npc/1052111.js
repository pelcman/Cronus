// 地下鉄のゴミ箱 1052111 (1号線-1区 103000101, JMS スクリプト名 givebubbleDoll3) — ゴミ箱の中に隠されたもの(20710)の進行中に調べると バブルリング人形(ゴミ箱)(4032136)
// が見つかる(1 つだけ)。出典 Reference/Cosmic/scripts/npc/1052111.js。台詞は創作。
function start() {
    if (player.hasQuest(20710) && !player.haveItem(4032136)) {
        player.gainItem(4032136, 1);
        cm.sendOk("（ゴミ箱の中から #b#t4032136##k を見つけた！　 #i4032136#）");
        return;
    }
    cm.sendOk("ただのゴミ箱だ。");
}
