// ハルモニア — 戦士4次転職 (Lv120+)
function start() {
    var job = player.getJob();
    var ok = (job == 111 || job == 121 || job == 131);
    if (!ok) {
        cm.sendOk("あなたを導くのは私の役目ではないようだ。");
        return;
    }
    if (player.getLevel() < 120) {
        cm.sendOk("4次転職にはレベル120以上が必要だ。");
        return;
    }
    if (cm.askYesNo("ついにここまで来たか。最後の転職を行うか?")) {
        player.setJob(job + 1);
        player.gainSp(1);
        cm.sendOk("おめでとう!あなたはこの道を極めた。");
    }
}
