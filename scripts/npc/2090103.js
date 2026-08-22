// 整形外科 — 顔の整形 (料金制、今の瞳の色は維持)
var COST = 20000;
function start() {
    var color = Math.floor(player.getFace() / 100) % 10 * 100;
    var bases = (player.getGender() == 0)
        ? [20000, 20001, 20002, 20003, 20004, 20005, 20006, 20007, 20008, 20009, 20010, 20011, 20012, 20013, 20014, 20016]
        : [21000, 21001, 21002, 21003, 21004, 21005, 21006, 21007, 21008, 21009, 21010, 21011, 21012, 21013, 21014, 21016];
    var styles = [];
    for (var i = 0; i < bases.length; i++) {
        if (player.isValidStyle(bases[i] + color)) {
            styles.push(bases[i] + color);
        } else if (player.isValidStyle(bases[i])) {
            styles.push(bases[i]);
        }
    }
    if (styles.length == 0) {
        cm.sendOk("すみません、今日は診療をお休みしています。");
        return;
    }
    var pick = cm.askAvatar("どんな顔立ちをご希望ですか?施術費は" + COST + "メルです。", styles);
    if (pick < 0 || pick >= styles.length) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。施術費は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.setFace(styles[pick]);
    cm.sendOk("手術は大成功です!新しい顔、よくお似合いですよ。");
}
