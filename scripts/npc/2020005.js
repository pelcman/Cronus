// アルケスタ 2020005 (エルナス 市場 211000100, JMS スクリプト名 oldBook1) — アルケスタと闇のクリスタル(3035)を終えた人にだけ 聖水 300 / 万病治療薬 400 /
// 魔法の石 5,000 / 召喚の石 5,000 メソ(Cosmic の値)で売る。個数入力(askNumber)は未実装のため選択式 [DEV]。出典 Reference/Cosmic/scripts/npc/2020005.js。台詞は創作。
var items = [[2050003, 300, "封印と呪いを解く薬"], [2050004, 400, "あらゆる状態異常を治す薬"], [4006000, 5000, "上位スキルに使う魔力の石"], [4006001, 5000, "上位スキルに使う召喚の石"]];
var counts = [1, 5, 10, 20, 50, 100];

function start() {
    if (!player.isQuestDone(3035)) {
        cm.sendOk("わしを手伝ってくれるなら、お礼に品物を売ってやろう。");
        return;
    }
    var menu = "お前のおかげで #b#t4031056##k は無事に封印された。もちろん、その代わりに 800 年ほどかけて蓄えた力の半分ほどを使い果たしたが…これで安らかに眠れる。ところで…珍しい品を探してはいないか？　骨折りの礼に、わしの持ち物をお前にだけ売ってやろう。欲しいものを選べ！";
    for (var i = 0; i < items.length; i++) {
        menu += "\r\n#L" + i + "# #b#t" + items[i][0] + "# (" + items[i][1] + " メソ)#k#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= items.length) {
        return;
    }
    var item = items[pick];
    var qmenu = "[DEV] #b#t" + item[0] + "##k で間違いないか？　" + item[2] + "だ。手に入りにくい品だが、安くしておこう。1 個 #b" + item[1] + " メソ#k。いくつ買う？#b";
    for (var j = 0; j < counts.length; j++) {
        qmenu += "\r\n#L" + j + "#" + counts[j] + " 個 (" + (item[1] * counts[j]) + " メソ)#l";
    }
    var q = cm.askMenu(qmenu);
    if (q < 0 || q >= counts.length) {
        cm.sendOk("買わないなら、売るものも無いな。");
        return;
    }
    var amount = counts[q];
    var total = item[1] * amount;
    if (!cm.askYesNo("本当に #r" + amount + " 個の #t" + item[0] + "##k を買うのか？　1 個 " + item[1] + " メソだから、合計 #r" + total + " メソ#k になるぞ。")) {
        cm.sendOk("そうか。ここにはいろいろな品がある。よく見て選ぶといい。お前にだけ売っているのだから、ぼったくったりはせん。");
        return;
    }
    if (player.getMeso() < total) {
        cm.sendOk("メソは足りているか？　#r" + total + "#k メソ持っているか確かめてくれ。");
        return;
    }
    player.gainMeso(-total);
    player.gainItem(item[0], amount);
    cm.sendOk("ありがとう。また品物が必要になったら、ここへ来るといい。年は取ったが、魔法の品を作るのはまだ朝飯前だ。");
}
