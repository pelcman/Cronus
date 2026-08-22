// リリン — アランの転職案内 (2100→2110→2111→2112)
function start() {
    var job = player.getJob();
    var lv = player.getLevel();
    if (job == 2000) {
        if (lv < 10) { cm.sendOk("記憶を取り戻すには、まだ力が足りないようですね。レベル10になったらまた来てください。"); return; }
        if (cm.askYesNo("戦士アランとしての力を取り戻しますか?")) {
            player.setJob(2100);
            player.gainMaxHp(200);
            player.gainSp(1);
            cm.sendOk("おめでとうございます!あなたは英雄アランとしての第一歩を踏み出しました。");
        }
        return;
    }
    if (job == 2100 && lv >= 30 && cm.askYesNo("さらなる記憶を取り戻しますか?(2次転職)")) {
        player.setJob(2110); player.gainMaxHp(250); player.gainSp(1);
        cm.sendOk("力が戻ってきましたね。おめでとうございます!");
        return;
    }
    if (job == 2110 && lv >= 70 && cm.askYesNo("さらなる記憶を取り戻しますか?(3次転職)")) {
        player.setJob(2111); player.gainMaxHp(300); player.gainSp(1);
        cm.sendOk("素晴らしい。英雄の力がよみがえっていきます。");
        return;
    }
    if (job == 2111 && lv >= 120 && cm.askYesNo("最後の記憶を取り戻しますか?(4次転職)")) {
        player.setJob(2112); player.gainMaxHp(400); player.gainSp(1);
        cm.sendOk("ついに……あなたは真の英雄アランに戻りました!");
        return;
    }
    cm.sendOk("マガイアの力に立ち向かえるのは、あなただけです。焦らず力をつけましょう。");
}
