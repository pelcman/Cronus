// コブシを開いて立て — 戦士1次転職 (job 100, Lv10+)
function start() {
    if (player.getJob() != 0) {
        cm.sendOk("既に道を選んだ者に、私から教えることはない。精進あるのみだ。");
        return;
    }
    if (player.getLevel() < 10) {
        cm.sendOk("戦士になるにはレベル10以上が必要だ。今のあなたはレベル" + player.getLevel() + "。もう少し鍛えてから来なさい。");
        return;
    }
    if (cm.askYesNo("戦士への転職を望むか?")) {
        player.setJob(100);
        player.gainSp(1);
        cm.sendOk("おめでとう!今日からあなたは戦士だ。SPを大切に使いなさい。");
    } else {
        cm.sendOk("焦ることはない。心が決まったらまた来なさい。");
    }
}
