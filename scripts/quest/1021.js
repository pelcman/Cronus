// ローザーとリンゴ (quest 1021, NPC 2000 ローザー / メイプルアイランド) — 初心者チュートリアル。HP を減らしてローザーのリンゴで
// 回復させ、回復したら贈り物を渡す。出典 Reference/Cosmic/scripts/quest/1021.js を JMS v186 に移植。JMS の Check/Act と照合済み:
// 開始スクリプト q1021s、終了スクリプト q1021e、終了条件はローザーのリンゴ 2010007。JMS の Act に報酬は無いので、Cosmic どおり
// スクリプトが渡す(リンゴ 2010000×3、緑リンゴ 2010009×3、EXP 10)。Cosmic の showInfo(UI/tutorial.img/28) は Cronus 未対応で省略。台詞は創作。
function start() {
    qm.sendNext("やあ、" + (player.getGender() == 0 ? "少年" : "お嬢さん") + "！　元気かい？　僕はローザー。新米メイプラーに色々と教えてあげる係さ。");
    qm.sendNext("誰に頼まれたのかって？　ははは、自分で決めたんだ！　新しい旅人には親切にしたくてね。");
    if (!qm.askAccept("それじゃあ、ちょっとした余興をひとつ。アバラカダブラ〜！")) {
        qm.sendOk("気が変わったらまた声をかけてくれ。");
        return;
    }
    if (player.getHp() >= 50) {
        player.setHp(25);
    }
    if (!player.haveItem(2010007)) {
        player.gainItem(2010007, 1);
    }
    player.startQuest(1021);
    qm.sendNext("驚いた？　HP が 0 になると大変なことになる。さあ、#rローザーのリンゴ#kをあげよう。食べると元気が出るよ。"
        + "アイテムウィンドウを開いてダブルクリックだ。ウィンドウはキーボードの #bI#k で開けるよ、簡単だろう？");
    qm.sendOk("もらったローザーのリンゴを全部食べてごらん。HP バーが増えるのが見えるはずだ。HP が 100% に戻ったらまた話しかけてくれ。");
}

function end() {
    if (player.getHp() < 50) {
        qm.sendOk("まだ HP が回復しきっていないね。あげたローザーのリンゴは全部食べたかい？");
        return;
    }
    qm.sendNext("アイテムを使うのは簡単だろう？　画面右下のスロットに #bホットキー#k を設定できるんだ。知らなかっただろう？"
        + "それと、初心者なら時間が経てば HP は自然に回復する。時間はかかるけど、初心者の作戦のひとつさ。");
    qm.sendNext("よし、いろいろ学んだね。旅に欠かせない贈り物をあげよう。緊急のときに使うんだよ！");
    qm.sendNext("これで僕が教えられることは全部だ。寂しいけどお別れだね。気をつけて、幸運を！\r\n\r\n"
        + "#fUI/UIWindow.img/QuestIcon/4/0#\r\n#v2010000# 3 #t2010000#\r\n#v2010009# 3 #t2010009#\r\n\r\n#fUI/UIWindow.img/QuestIcon/8/0# 10 exp");
    player.gainExp(10);
    player.gainItem(2010000, 3);
    player.gainItem(2010009, 3);
    player.completeQuest(1021);
}
