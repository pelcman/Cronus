// 青色の花山 1063001 (忍耐の森「4段階」 105040313) — ゾーンの花箱/プレゼント(2053)の進行中は #t4031026# を 20 本摘める。そうでなければ鉱石を少し拾い、
// どちらもスリーピーウッド 105040300 へ戻される(忍耐の森の踏破報酬)。出典 Reference/Cosmic/scripts/npc/1063001.js。台詞は創作。
var prizes = [[4020000, 4], [4020002, 4], [4020006, 4]];

function start() {
    if (player.hasQuest(2053) && player.itemQuantity(4031026) < 20) {
        player.gainItem(4031026, 20 - player.itemQuantity(4031026));
        cm.sendOk("（#b#t4031026##k を 20 本摘んだ。ここまで来た甲斐があった。）");
    } else {
        var prize = prizes[Math.floor(Math.random() * prizes.length)];
        player.gainItem(prize[0], prize[1]);
        cm.sendOk("（花の間に鉱石が落ちていた。#b#t" + prize[0] + "##k を拾った。）");
    }
    player.warp(105040300, 0);
}
