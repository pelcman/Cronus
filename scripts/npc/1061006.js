// 変な形の石像 1061006 (スリーピーウッド 105040300, JMS スクリプト名 flower_in) — ゾーンの花箱(桃)(2052)・プレゼント(2053)・最後のプレゼント(2054)の
// 進行/完了に応じて 忍耐の森「1/3/5段階」(105040310 / 105040312 / 105040314) へ送る。出典 Reference/Cosmic/scripts/npc/1061006.js。台詞は創作。
var maps = [105040310, 105040312, 105040314];
var names = ["忍耐の森「1段階」", "忍耐の森「3段階」", "忍耐の森「5段階」"];

function start() {
    cm.sendNext("石像から、不思議な力を感じる…。");
    var zones = 0;
    if (player.hasQuest(2054) || player.isQuestDone(2054)) {
        zones = 3;
    } else if (player.hasQuest(2053) || player.isQuestDone(2053)) {
        zones = 2;
    } else if (player.hasQuest(2052) || player.isQuestDone(2052)) {
        zones = 1;
    }
    if (zones == 0) {
        return;
    }
    var menu = "その力は、森の奥深くへと導いてくれるようだ。#b";
    for (var i = 0; i < zones; i++) {
        menu += "\r\n#L" + i + "#" + names[i] + "#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= zones) {
        return;
    }
    player.warp(maps[pick], 0);
}
