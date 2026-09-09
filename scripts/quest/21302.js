// 紅玉作り (quest 21302, 受注 NPC 1201000 リリン → 完了 NPC 1201002 マッハ, Lv70) — 作り直した 紅朱珠(4032312) をマッハに戻し、アラン 2次(2110)から 3次(2111)へ。
// 勲章 試練の中のアランの勲章(1142131) は JMS では 29926 が渡す。出典 Reference/Cosmic/scripts/quest/21302.js。JMS: 終了スクリプト q21302e、
// Check は 4032312×1、Act は空(紅朱珠の回収はスクリプト側)。台詞は創作。
function end() {
    if (!player.haveItem(4032312)) {
        qm.sendOk("#b#t4032312##k はまだか？　あれがないと、俺は元に戻れない。");
        return;
    }
    qm.sendNext("待て…それは…。紅玉の作り方を思い出したのか？　#t4032312# だ！");
    qm.sendNext("よし、紅玉を元の場所に戻せば、俺の力も戻る。ついでに、お前の力ももう少し引き出してやろう。");
    if (player.getJob() != 2110) {
        qm.sendOk("（マッハの力はもう戻っている。）");
        return;
    }
    player.gainItem(4032312, -1);
    player.changeJob(2111);
    player.completeQuest(21302);
    qm.sendOk("さあ、修行を続けろ。失った技を全部取り戻すまでな。");
}
