// [DEV] 転職の本来の経路はクエスト(シグナス: quest/20101〜20105・20201〜20205・20311〜20315・20408 / アラン: 21101・21201・21302)。
// この NPC から直接の転職は、実機でクエスト経路を確認できるまでの近道として残している。
// イカルト — 闇の騎士団長 (夜の騎士への転職: 1400→1410→1411)
function start() {
    var job = player.getJob();
    var lv = player.getLevel();
    if (job == 1000) {
        if (lv < 10) { cm.sendOk("騎士になるにはレベル10以上が必要だ。まずは修練を積みなさい。"); return; }
        if (cm.askYesNo("[DEV] 夜の騎士への転職を望むか?")) {
            player.changeJob(1400);
            cm.sendOk("おめでとう!今日から君は夜の騎士だ。シグナス女王のために戦おう。");
        }
        return;
    }
    if (job == 1400) {
        if (lv < 30) { cm.sendOk("2次転職にはレベル30以上が必要だ。"); return; }
        if (cm.askYesNo("[DEV] その力、確かなものだ。2次転職を行うか?")) {
            player.changeJob(1410);
            cm.sendOk("おめでとう!さらなる高みを目指しなさい。");
        }
        return;
    }
    if (job == 1410) {
        if (lv < 70) { cm.sendOk("3次転職にはレベル70以上が必要だ。"); return; }
        if (cm.askYesNo("[DEV] 見事な成長だ。3次転職を行うか?")) {
            player.changeJob(1411);
            cm.sendOk("おめでとう!君は騎士団の誇りだ。");
        }
        return;
    }
    cm.sendOk("シグナス騎士団は常に君を歓迎している。");
}
