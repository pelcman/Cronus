// ハインズ — 魔法使い1次転職 (job 200, Lv8+)
function start() {
    if (player.getJob() != 0) {
        cm.sendOk("既に道を選んだ者に、私から教えることはない。精進あるのみだ。");
        return;
    }
    if (player.getLevel() < 8) {
        cm.sendOk("魔法使いになるにはレベル8以上が必要だ。今のあなたはレベル" + player.getLevel() + "。もう少し鍛えてから来なさい。");
        return;
    }
    if (cm.askYesNo("魔法使いへの転職を望むか?")) {
        player.setJob(200);
        player.gainMaxMp(150);
        player.gainSp(1);
        cm.sendOk("おめでとう!今日からあなたは魔法使いだ。SPを大切に使いなさい。");
    } else {
        cm.sendOk("焦ることはない。心が決まったらまた来なさい。");
    }
}
