// ジェーン 1002100 (港口 104000000, JMS スクリプト名 jane) — ジェーンの最後の挑戦(2013)を手伝った人に手作りの薬を売る。
// 白い薬 310 / ウナギ焼 1060 / ミネラルウォーター 1600 / スイカ 3120 メソ(Cosmic の値)。個数入力(askNumber)は未実装のため選択式 [DEV]。
// 出典 Reference/Cosmic/scripts/npc/1002100.js。台詞は創作。
var items = [[2000002, 310, "HP 300 回復"], [2022003, 1060, "HP 1000 回復"], [2022000, 1600, "MP 800 回復"], [2001000, 3120, "HP・MP 1000 回復"]];
var counts = [1, 5, 10, 20, 50, 100];

function start() {
    if (!player.isQuestDone(2013)) {
        if (player.isQuestDone(2010)) {
            cm.sendOk("あなたにはまだ、私の薬を買えるほどの力はなさそう…。");
            return;
        }
        cm.sendOk("私の夢は、あなたみたいに世界中を旅すること。でも父さんは危ないからって許してくれないの。私が思っているほど弱くないって証明できれば、許してくれるかもしれないけど…。");
        return;
    }
    cm.sendNext("あなたね…あなたのおかげで、いろいろ進んだわ。最近は薬をたくさん作っているの。必要なものがあれば言ってね。");
    var menu = "どれを買う？#b";
    for (var i = 0; i < items.length; i++) {
        menu += "\r\n#L" + i + "##i" + items[i][0] + "# #t" + items[i][0] + "# (" + items[i][1] + " メソ)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= items.length) {
        return;
    }
    var item = items[pick];
    var qmenu = "[DEV] #b#t" + item[0] + "##k ね？　" + item[2] + "よ。いくつ買う？#b";
    for (var j = 0; j < counts.length; j++) {
        qmenu += "\r\n#L" + j + "#" + counts[j] + " 個 (" + (item[1] * counts[j]) + " メソ)#l";
    }
    var q = cm.askMenu(qmenu);
    if (q < 0 || q >= counts.length) {
        return;
    }
    var amount = counts[q];
    var total = item[1] * amount;
    if (!cm.askYesNo("#r" + amount + "#k 個の #b#t" + item[0] + "##k を買うのね？　1 個 " + item[1] + " メソだから、合計 #r" + total + "#k メソよ。")) {
        cm.sendOk("材料はまだたくさんあるから、ゆっくり選んでね。");
        return;
    }
    if (player.getMeso() < total) {
        cm.sendOk("メソが足りないみたい。#r" + total + "#k メソ持っているか、確かめてね。");
        return;
    }
    player.gainMeso(-total);
    player.gainItem(item[0], amount);
    cm.sendOk("ありがとう。薬はいつでも作れるから、必要になったらまた来てね。");
}
