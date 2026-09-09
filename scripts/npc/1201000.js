// [DEV] 転職の本来の経路はクエスト(シグナス: quest/20101〜20105・20201〜20205・20311〜20315・20408 / アラン: 21101・21201・21302)。
// この NPC から直接の転職は、実機でクエスト経路を確認できるまでの近道として残している。
// リリン — アランの転職案内 (2100→2110→2111→2112)
function start() {
    var job = player.getJob();
    var lv = player.getLevel();
    if (job == 2000) {
        if (lv < 10) { cm.sendOk("記憶を取り戻すには、まだ力が足りないようですね。レベル10になったらまた来てください。"); return; }
        if (cm.askYesNo("[DEV] 戦士アランとしての力を取り戻しますか?")) {
            player.changeJob(2100);
            cm.sendOk("おめでとうございます!あなたは英雄アランとしての第一歩を踏み出しました。");
        }
        return;
    }
    if (job == 2100 && lv >= 30 && cm.askYesNo("[DEV] さらなる記憶を取り戻しますか?(2次転職)")) {
        player.changeJob(2110);
        cm.sendOk("力が戻ってきましたね。おめでとうございます!");
        return;
    }
    if (job == 2110 && lv >= 70 && cm.askYesNo("[DEV] さらなる記憶を取り戻しますか?(3次転職)")) {
        player.changeJob(2111);
        cm.sendOk("素晴らしい。英雄の力がよみがえっていきます。");
        return;
    }
    if (job == 2111 && lv >= 120 && cm.askYesNo("[DEV] 最後の記憶を取り戻しますか?(4次転職)")) {
        player.changeJob(2112);
        cm.sendOk("ついに……あなたは真の英雄アランに戻りました!");
        return;
    }
    cm.sendOk("マガイアの力に立ち向かえるのは、あなただけです。焦らず力をつけましょう。");
}
