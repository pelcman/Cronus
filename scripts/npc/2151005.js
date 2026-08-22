// イリーナ — 風の騎士団長 (風の騎士への転職: 1300→1310→1311)
function start() {
    var job = player.getJob();
    var lv = player.getLevel();
    if (job == 1000) {
        if (lv < 10) { cm.sendOk("騎士になるにはレベル10以上が必要だ。まずは修練を積みなさい。"); return; }
        if (cm.askYesNo("風の騎士への転職を望むか?")) {
            player.setJob(1300);
            player.gainMaxHp(200);
            player.gainSp(1);
            cm.sendOk("おめでとう!今日から君は風の騎士だ。シグナス女王のために戦おう。");
        }
        return;
    }
    if (job == 1300) {
        if (lv < 30) { cm.sendOk("2次転職にはレベル30以上が必要だ。"); return; }
        if (cm.askYesNo("その力、確かなものだ。2次転職を行うか?")) {
            player.setJob(1310);
            player.gainMaxHp(250);
            player.gainSp(1);
            cm.sendOk("おめでとう!さらなる高みを目指しなさい。");
        }
        return;
    }
    if (job == 1310) {
        if (lv < 70) { cm.sendOk("3次転職にはレベル70以上が必要だ。"); return; }
        if (cm.askYesNo("見事な成長だ。3次転職を行うか?")) {
            player.setJob(1311);
            player.gainMaxHp(300);
            player.gainSp(1);
            cm.sendOk("おめでとう!君は騎士団の誇りだ。");
        }
        return;
    }
    cm.sendOk("シグナス騎士団は常に君を歓迎している。");
}
