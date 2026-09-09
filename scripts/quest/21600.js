// プニの頼み (quest 21600, 受注 NPC 1202007 プニ → 完了 NPC 2060000 ナヌク, Lv50) — オオカミの騎乗の話。承諾で受注(完了側はデータ経路)。
// 出典 Reference/Cosmic/scripts/quest/21600.js。JMS: 開始スクリプト q21600s、normalAutoStart、Act は空。台詞は創作。
function start() {
    qm.sendNext("やあ、アラン。あの頃よりずいぶん強くなったみたいだね。それなら、そろそろ #bオオカミ#k に乗れるかもしれない。");
    if (!qm.askAccept("興味がある？　よし、まずは氷の頂上にいる #bナヌク#k に会って、オオカミに認めてもらう必要があるんだ。行ってくれる？")) {
        return;
    }
    player.startQuest(21600);
    qm.sendOk("会うべき相手は #bナヌク#k。彼女は雪山の頂上にいる。オオカミのことなら、彼女が一番よく知っているよ。");
}
