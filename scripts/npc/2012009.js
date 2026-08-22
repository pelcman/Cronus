// 整形外科補助 — 瞳の色の変更 (料金制、顔立ちはそのまま)
var COST = 10000;
function start() {
    var face = player.getFace();
    var style = face - Math.floor(face / 100) % 10 * 100;
    var styles = [];
    for (var c = 0; c < 9; c++) {
        if (player.isValidStyle(style + c * 100)) {
            styles.push(style + c * 100);
        }
    }
    if (styles.length <= 1) {
        cm.sendOk("ごめんなさい、その顔立ちは瞳の色を変えられないみたいです。");
        return;
    }
    var pick = cm.askAvatar("どの瞳の色にしますか?施術費は" + COST + "メルです。", styles);
    if (pick < 0 || pick >= styles.length) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。施術費は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.setFace(styles[pick]);
    cm.sendOk("はい、終わりましたよ。素敵な瞳になりましたね!");
}
