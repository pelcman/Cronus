// しわくちゃになった紙切れまた探し (quest 2215, 完了 NPC 1052108 倒れたコミ箱) — 夕方(17〜20時)に 2,000 メソを納めて紙切れを得る。
// 出典 Reference/Cosmic/scripts/quest/2215.js。JMS: 終了スクリプト q2215e、Act1 で money -2000 と 4031894 を付与(完了処理で処理)。
// メソ不足だと Act の減算で残高が 0 に切り詰められるので、先に残高を確かめる。台詞は創作。
function end() {
    var h = player.hourOfDay();
    if (h < 17 || h >= 20) {
        qm.sendOk("（うーん、ゴミ箱を探してみたけど、ゼイエムが言っていた #t4031894# は見当たらない…まだ時間じゃないのかも。）");
        return;
    }
    if (player.getMeso() < 2000) {
        qm.sendOk("（あ、納めるお金がまだ足りない。）");
        return;
    }
    qm.sendNext("（よし、ここに料金を入れて紙切れをもらおう…これでいい、できた。）");
    player.completeQuest(2215);
}
