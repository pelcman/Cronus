// ナイトウォーカーの騎士クラス試験 (quest 20204, NPC 1101006 イカルト) — テストの証票(4032099)を 30 枚集めた見習い騎士(1400)の 2次転職(1410)。
// 1次のスキルに SP を使い切っていないと断られる(Lv30 時点の余り SP は (Lv-30)*3 まで)。勲章 正騎士の勲章(1142067) は JMS では 29907 が渡す。
// 出典 Reference/Cosmic/scripts/quest/20204.js。JMS: 終了スクリプト q20204e、Check は 20200 完了・Lv30・4032099×30、Act は空(証票の回収はスクリプト側)。台詞は創作。
function end() {
    if (!qm.askYesNo("#t4032099# を全部集めてきたのか…よし。そなたが正騎士にふさわしいことは分かった。試験の結果を受け取る準備はいいか？")) {
        qm.sendOk("まだ正騎士の責任を負う覚悟ができていないようだな。準備ができたら、また来なさい。");
        return;
    }
    if (player.getJob() == 1400 && player.getSp() > (player.getLevel() - 30) * 3) {
        qm.sendOk("#bSP#k が余りすぎている。1次のスキルにもっと SP を使ってから来なさい。");
        return;
    }
    if (player.getJob() != 1400) {
        qm.sendOk("そなたはもう正騎士ではないか。");
        return;
    }
    if (player.itemQuantity(4032099) < 30) {
        qm.sendOk("#b#t4032099##k を 30 枚集めてくるのだ。");
        return;
    }
    player.gainItem(4032099, -30);
    player.changeJob(1410);
    player.completeQuest(20204);
    qm.sendNext("そなたはもう見習い騎士ではない。今この瞬間から、正式な #bナイトウォーカー#k、シグナス騎士団の正騎士だ。");
    qm.sendNext("#bSP#k を渡しておいた。新しいスキルも増えている。スキルウィンドウで確かめなさい。");
    qm.sendOk("正騎士になったからには、それにふさわしく振る舞うのだ。女王のため、メイプルワールドのために。");
}
