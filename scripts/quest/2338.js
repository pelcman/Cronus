// 殺キノコスプレーが欲しい！ (quest 2338, NPC 1300007 スカス) — 殺キノコスプレー(2430014)を失くした人に予備を渡す。JMS Check: 2318 完了、
// 2322 が記録にあり、2430014 を持っていないこと。出典 Reference/Cosmic/scripts/quest/2338.js。JMS: 開始スクリプト q2338s、Act は空
// (スプレーの付与と完了はスクリプト側)。台詞は創作。
function start() {
    if (player.haveItem(2430014)) {
        qm.sendOk("#b#t2430014##k なら、まだ持っているじゃないか。");
        return;
    }
    qm.sendNext("#b#t2430014##k を使ってしまったのか？　まあいい、予備を作っておいたんだ。持っていけ。");
    player.gainItem(2430014, 1);
    player.completeQuest(2338);
}
