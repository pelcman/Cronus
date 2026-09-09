// 足りない薬材探し (quest 3833, NPC 2092000 クーおじいさん, 武陵) — しなびたキキョウ(4000294)を届けた数で報酬が変わる。
// JMS: 終了スクリプト q3833e、Check[1] は 4000294×1 以上、Act は空。段階と報酬は Cosmic の値(1000: 素早さの書(全身鎧)60% + パワーエリクサー×50 +
// EXP 54000 / 600: トナカイの乳×50 + 54000 / 500: 54000 / 100: 45000 / 50: するめ×50 + 10000 / 1: 赤い薬 + 10)。台詞は創作。
function end() {
    var n = player.itemQuantity(4000294);
    if (n < 1) {
        qm.sendOk("#b#t4000294##k は見つかったかの？　一本でもよいから持ってきてくれんか。");
        return;
    }
    qm.sendNext("おお、わしの探していた薬材じゃ！　持ってきてくれた分に応じて、お礼をさせてもらおう。");
    if (n >= 1000) {
        player.gainItem(4000294, -1000);
        player.gainItem(2040501, 1);
        player.gainItem(2000005, 50);
        player.gainExp(54000);
    } else if (n >= 600) {
        player.gainItem(4000294, -600);
        player.gainItem(2020013, 50);
        player.gainExp(54000);
    } else if (n >= 500) {
        player.gainItem(4000294, -500);
        player.gainExp(54000);
    } else if (n >= 100) {
        player.gainItem(4000294, -100);
        player.gainExp(45000);
    } else if (n >= 50) {
        player.gainItem(4000294, -50);
        player.gainItem(2020007, 50);
        player.gainExp(10000);
    } else {
        player.gainItem(4000294, -1);
        player.gainItem(2000000, 1);
        player.gainExp(10);
    }
    player.completeQuest(3833);
}
