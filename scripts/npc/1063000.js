// 桃色の花山 1063000 (忍耐の森「2段階」 105040311) — ゾーンの花箱/プレゼント(2052)の進行中は #t4031025# を 10 本摘める。そうでなければ鉱石を少し拾い、
// どちらもスリーピーウッド 105040300 へ戻される(忍耐の森の踏破報酬)。出典 Reference/Cosmic/scripts/npc/1063000.js。台詞は創作。
var prizes = [[4010000, 3], [4010001, 3], [4010002, 3], [4010003, 3], [4010004, 3], [4010005, 3]];

function start() {
    if (player.hasQuest(2052) && player.itemQuantity(4031025) < 10) {
        player.gainItem(4031025, 10 - player.itemQuantity(4031025));
        cm.sendOk("（#b#t4031025##k を 10 本摘んだ。ここまで来た甲斐があった。）");
    } else {
        var prize = prizes[Math.floor(Math.random() * prizes.length)];
        player.gainItem(prize[0], prize[1]);
        cm.sendOk("（花の間に鉱石が落ちていた。#b#t" + prize[0] + "##k を拾った。）");
    }
    player.warp(105040300, 0);
}
