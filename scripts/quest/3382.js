// ユレテの報答 (quest 3382, NPC 2112014 ユレテ, マガティア, Lv70〜85, 繰り返し可) — ジェニミストの玉(4001159)とアルカドノの玉(4001160)を
// 集めて渡す。25 個ずつで ホルスの目(1122010、未所持のとき)、10 個ずつで 知恵の石(2041212)。JMS: 終了スクリプト q3382e、Act は空。
// 出典 Reference/Cosmic/scripts/quest/3382.js(報酬も同じ; JMS でアイテムの存在を確認済み)。台詞は創作。
function end() {
    var a = player.itemQuantity(4001159);
    var b = player.itemQuantity(4001160);
    if (a >= 25 && b >= 25 && !player.haveItem(1122010)) {
        player.gainItem(4001159, -25);
        player.gainItem(4001160, -25);
        player.gainItem(1122010, 1);
        qm.sendOk("玉を取り戻してくれてありがとう。お礼に、この #b#t1122010##k を受け取ってくれ。");
        player.completeQuest(3382);
        return;
    }
    if (a >= 10 && b >= 10) {
        player.gainItem(4001159, -10);
        player.gainItem(4001160, -10);
        player.gainItem(2041212, 1);
        qm.sendOk("玉を取り戻してくれてありがとう。この #b#t2041212##k は、私の研究の副産物だ。役立ててくれ。");
        player.completeQuest(3382);
        return;
    }
    qm.sendNext("お礼をするには、#b#t4001159##k と #b#t4001160##k が少なくとも 10 個ずつ必要だ。");
}
