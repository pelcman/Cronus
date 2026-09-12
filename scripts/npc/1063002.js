// 白色の花山 1063002 (忍耐の森「7段階」 105040316) — ゾーンの花箱/プレゼント(2054)の進行中は #t4031028# を 30 本摘める。そうでなければ鉱石を少し拾い、
// どちらもスリーピーウッド 105040300 へ戻される(忍耐の森の踏破報酬)。出典 Reference/Cosmic/scripts/npc/1063002.js。台詞は創作。
var prizes = [[4010006, 4], [4010007, 4], [4020007, 4]];

function start() {
    if (player.hasQuest(2054) && player.itemQuantity(4031028) < 30) {
        player.gainItem(4031028, 30 - player.itemQuantity(4031028));
        cm.sendOk("（#b#t4031028##k を 30 本摘んだ。ここまで来た甲斐があった。）");
    } else {
        var prize = prizes[Math.floor(Math.random() * prizes.length)];
        player.gainItem(prize[0], prize[1]);
        cm.sendOk("（花の間に鉱石が落ちていた。#b#t" + prize[0] + "##k を拾った。）");
    }
    player.warp(105040300, 0);
}
