// 美容助手 — ヘアカラー変更 (料金制、髪型はそのまま)
var COST = 8000;
function start() {
    var base = Math.floor(player.getHair() / 10) * 10;
    var styles = [];
    for (var c = 0; c < 8; c++) {
        if (player.isValidStyle(base + c)) {
            styles.push(base + c);
        }
    }
    if (styles.length <= 1) {
        cm.sendOk("ごめんなさい、その髪型は染められないみたいです。先に髪型を変えてみてはどうですか?");
        return;
    }
    var pick = cm.askAvatar("どの色に染めますか?お会計は" + COST + "メルです。", styles);
    if (pick < 0 || pick >= styles.length) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。料金は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.setHair(styles[pick]);
    cm.sendOk("きれいに染まりましたよ!よくお似合いです。");
}
