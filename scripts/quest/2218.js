// ネルラの情報 (quest 2218, NPC 1052103) — カニングシティの聞き込み: もみくちゃの紙切れ(4031894)を持って話すと情報が聞けて完了する。
// 出典 Reference/Cosmic/scripts/quest/2218.js。JMS: 開始スクリプト q2218s、Act に報酬は無いので Cosmic どおり EXP 7000 をスクリプトで
// 付与(数値は Cosmic の値)。2216〜2219 が全部済むと紙切れを回収する(Cosmic どおり)。台詞は創作。
function start() {
    qm.sendNext("ねえ、最近の #rラケリス#k の様子がおかしいと思わない？　何が起きているのか調べた方がいいわ。あの行動は妙よ。");
    player.completeQuest(2218);
    player.gainExp(7000);
    if (player.isQuestDone(2216) && player.isQuestDone(2217) && player.isQuestDone(2218) && player.isQuestDone(2219) && player.haveItem(4031894)) {
        player.gainItem(4031894, -1);
    }
}
