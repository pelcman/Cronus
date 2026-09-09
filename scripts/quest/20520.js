// 騎士の品位 (quest 20520, NPC 1101002 ナインハート) — Lv50 の騎士に騎士団専用の乗り物(ミミアナ)のことを教える。その場で受注→完了。
// 出典 Reference/Cosmic/scripts/quest/20520.js。JMS: 開始スクリプト q20520s、normalAutoStart、Lv50、Act は空。台詞は創作。
function start() {
    qm.sendNext("おお、もうレベル 50 か。それなのに、まだ歩いて旅をしているのか？　騎士団には、騎士だけが乗れる特別な乗り物がある。");
    if (!qm.askAccept("興味があるか？　乗り物を育てるのは手間もかかるが、それも騎士の品位というものだ。")) {
        return;
    }
    player.startQuest(20520);
    player.completeQuest(20520);
    qm.sendOk("シグナス騎士団だけの乗り物 #bミミアナ#k だ。詳しくは、エレヴの調教師 #bキリド#k に聞くといい。");
}
