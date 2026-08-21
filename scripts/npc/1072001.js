// 魔法使い転職教官 — 2次転職 (Lv30+)
function start() {
    var job = player.getJob();
    if (job == 200) {
        if (player.getLevel() < 30) {
            cm.sendOk("2次転職にはレベル30以上が必要だ。今はレベル" + player.getLevel() + "だな。");
            return;
        }
        var jobs = [210, 220, 230];
        var names = ["ウィザード(火・毒)", "ウィザード(氷・雷)", "クレリック"];
        var menu = "力を認めよう。進む道を選びなさい。";
        for (var i = 0; i < names.length; i++) {
            menu += "\r\n#L" + i + "#" + names[i] + "#l";
        }
        var pick = cm.askMenu(menu);
        if (cm.askYesNo(names[pick] + "への転職でいいのか?")) {
            player.setJob(jobs[pick]);
            player.gainSp(1);
            cm.sendOk("おめでとう!今日から" + names[pick] + "だ。");
        } else {
            cm.sendOk("よく考えてから決めなさい。");
        }
        return;
    }
    if (job == 0) {
        cm.sendOk("まずは1次転職を済ませてから来なさい。");
        return;
    }
    cm.sendOk("あなたに教えることはないようだ。");
}
