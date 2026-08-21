// タイラス — 戦士3次転職 (Lv70+)
function start() {
    var job = player.getJob();
    var ok = (job == 110 || job == 120 || job == 130);
    if (!ok) {
        cm.sendOk("あなたを導くのは私の役目ではないようだ。");
        return;
    }
    if (player.getLevel() < 70) {
        cm.sendOk("3次転職にはレベル70以上が必要だ。");
        return;
    }
    if (cm.askYesNo("その力、確かに見届けた。3次転職を行うか?")) {
        player.setJob(job + 1);
        player.gainMaxHp(300);
        player.gainSp(1);
        cm.sendOk("おめでとう!新たな力を存分に振るいなさい。");
    }
}
