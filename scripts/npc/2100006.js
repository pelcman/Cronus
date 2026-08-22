// 美容師 — ヘアスタイル変更 (料金制、今の髪色は維持)
var COST = 15000;
function start() {
    var color = player.getHair() % 10;
    var bases = (player.getGender() == 0)
        ? [30000, 30020, 30030, 30040, 30050, 30060, 30100, 30110, 30120, 30130, 30140, 30150, 30160, 30170, 30180, 30190, 30200, 30210, 30220, 30230, 30240, 30250]
        : [31000, 31010, 31020, 31030, 31040, 31050, 31060, 31070, 31080, 31090, 31100, 31110, 31120, 31130, 31140, 31150, 31160, 31170, 31180, 31190, 31200, 31210];
    var styles = [];
    for (var i = 0; i < bases.length; i++) {
        if (player.isValidStyle(bases[i] + color)) {
            styles.push(bases[i] + color);
        } else if (player.isValidStyle(bases[i])) {
            styles.push(bases[i]);
        }
    }
    if (styles.length == 0) {
        cm.sendOk("ごめんなさい、今日はお店を開けられないんです。また今度来てくださいね。");
        return;
    }
    var pick = cm.askAvatar("いらっしゃいませ!今日はどんな髪型にしますか?お会計は" + COST + "メルです。", styles);
    if (pick < 0 || pick >= styles.length) {
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。料金は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.setHair(styles[pick]);
    cm.sendOk("はい、できあがり!とてもお似合いですよ。またのご来店をお待ちしています。");
}
