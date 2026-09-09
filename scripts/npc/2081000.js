// 村長タタモ (Tatamo, NPC 2081000, リプレ 240000000) — 魔法の種(4031346)を 1個 30,000 メソで売る。
// 出典 Reference/Cosmic/scripts/npc/2081000.js を JMS v186 に移植。魔法の種 4031346 は JMS String.wz に実在。
// Cosmic は数量を数値入力(sendGetNumber)で受けるが、JMSv186 リファレンスに数値入力ダイアログの実装が無く
// (getNPCTalkNum が "not coded")、Cronus にも無いので数量は選択式に簡略化し、その旨を [DEV] で明示する。
// 「リプレのために何かする」は Cosmic でも Under development。台詞は創作。
var SEED = 4031346;
var PRICE = 30000;
var AMOUNTS = [1, 5, 10, 20, 50];

function start() {
    var sel = cm.askMenu("…何か用かね？\r\n#b#L0#魔法の種を買う#l\r\n#L1#リプレのために何かする#l#k");
    if (sel == 1) {
        cm.sendDev("リプレの手伝いはまだ用意できていない。");
        return;
    }
    if (sel != 0) {
        return;
    }

    cm.sendNext("この町の者ではないようだな。#b#t" + SEED + "##k が欲しいのか？　貴重な品だ、ただでは渡せぬ。1個 #b" + PRICE + " メソ#k で譲ろう。");

    var menu = "[DEV] 数量は選択式です。いくつ買うかね？";
    for (var i = 0; i < AMOUNTS.length; i++) {
        menu += "\r\n#L" + i + "#" + AMOUNTS[i] + " 個（" + (AMOUNTS[i] * PRICE) + " メソ）#l";
    }
    var a = cm.askMenu(menu);
    if (a < 0 || a >= AMOUNTS.length) {
        return;
    }

    var qty = AMOUNTS[a];
    var cost = qty * PRICE;
    if (!cm.askYesNo("#b#t" + SEED + "# " + qty + " 個#k を #b" + cost + " メソ#k で買う。よいか？")) {
        cm.sendOk("よく考えて決めるのだ。決まったらまた声をかけてくれ。");
        return;
    }
    if (player.getMeso() < cost) {
        cm.sendOk("メソが足りないようだ。持ち物の空きも確かめておくれ。");
        return;
    }

    player.gainMeso(-cost);
    player.gainItem(SEED, qty);
    cm.sendOk("またおいで。");
}
