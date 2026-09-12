// タンユン 1092000 (ノーチラス 食堂 120000103, JMS スクリプト名 nautil_cow) — 新鮮な牛乳を手に入れる(2180)の進行中に、空のミルクボトル(4031847)を渡して
// 牛小屋 912000100 へ送る。出典 Reference/Cosmic/scripts/npc/1092000.js。台詞は創作。
function start() {
    if (!player.hasQuest(2180)) {
        cm.sendOk("うちの料理は新鮮な牛乳が命なんだ。牛小屋の牛たちのおかげさ。");
        return;
    }
    cm.sendNext("よし、うちの牛のいる牛小屋へ送ってやろう。牛乳を全部飲んでしまう子牛には気をつけろよ。せっかくの苦労が水の泡になるからな。");
    cm.sendNext("子牛と母牛は、ひと目では見分けにくい。生まれて一、二か月の子牛でも、もう母牛と同じ大きさに育っている。見た目もそっくりで…俺でも時々間違えるくらいだ。幸運を祈るよ！");
    if (!player.haveItem(4031847) && !player.haveItem(4031848) && !player.haveItem(4031849) && !player.haveItem(4031850)) {
        player.gainItem(4031847, 1);
    }
    player.warp(912000100, 0);
}
