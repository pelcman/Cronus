// カイリン — 海賊の転職を一括で担当 (身内サーバー向け簡略化)
function start() {
    var job = player.getJob();
    var lv = player.getLevel();
    if (job == 0) {
        if (lv < 10) {
            cm.sendOk("海賊になるにはレベル10以上が必要よ。");
            return;
        }
        if (cm.askYesNo("海賊への転職を望む?")) {
            player.setJob(500);
        player.gainMaxHp(150);
            player.gainSp(1);
            cm.sendOk("ようこそ、海賊の世界へ!");
        }
        return;
    }
    if (job == 500 && lv >= 30) {
        var pick = cm.askMenu("進む道を選びなさい。\r\n#L0#インファイター#l\r\n#L1#ガンスリンガー#l");
        player.setJob(pick == 0 ? 510 : 520);
        player.gainSp(1);
        cm.sendOk("おめでとう!");
        return;
    }
    if ((job == 510 || job == 520) && lv >= 70) {
        if (cm.askYesNo("3次転職を行う?")) {
            player.setJob(job + 1);
            player.gainSp(1);
            cm.sendOk("おめでとう!");
        }
        return;
    }
    if ((job == 511 || job == 521) && lv >= 120) {
        if (cm.askYesNo("最後の転職を行う?")) {
            player.setJob(job + 1);
            player.gainSp(1);
            cm.sendOk("おめでとう!あなたは海賊の頂点に立った。");
        }
        return;
    }
    cm.sendOk("今は教えることがないわ。腕を磨いてきなさい。");
}
