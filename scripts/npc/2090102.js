// 皮膚管理師 — 肌の色の変更 (料金制)
var COST = 5000;
function start() {
    var styles = [];
    for (var c = 0; c < 12; c++) {
        if (player.isValidStyle(c)) {
            styles.push(c);
        }
    }
    if (styles.length == 0) {
        cm.sendOk("ごめんなさい、今日はお手入れができないんです。");
        return;
    }
    var pick = cm.askAvatar("どの肌の色になさいますか?お手入れ代は" + COST + "メルです。", styles);
    if (pick < 0 || pick >= styles.length) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。お手入れ代は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.setSkin(styles[pick]);
    cm.sendOk("お手入れ完了です!健康的でいい色ですね。");
}
