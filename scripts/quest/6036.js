// メイカースキルの ID は職業系統ごと: 冒険家 1007、シグナス 10001007、アラン 20001007 (Skill.wz 000/1000/2000; Cosmic と同じ計算)。
function makerSkill() {
    return Math.floor(player.getJob() / 1000) * 10000000 + 1007;
}

// 意外な結果 (quest 6036, NPC 2110004 マレン, Lv105) — ゴールドアンビル(4031980)を作ったと知って驚き、メイカーを Lv3 に上げてくれる。
// EXP 300000 は Cosmic の値(JMS の Act は 落書きだらけの紙くず 4031979 の付与)。出典 Reference/Cosmic/scripts/quest/6036.js。
// JMS: 終了スクリプト q6036e、Check は 6035 完了・4031980×1。台詞は創作。
function end() {
    qm.sendNext("また邪魔をしに来たのか？　何の用だ？");
    if (!player.haveItem(4031980)) {
        qm.sendOk("…どいてくれ。邪魔をされては仕事が終わらん。");
        return;
    }
    qm.sendNext("#b#t4031980##k を作っただと？！　どうやって…いったいどうやったんだ？　…まあいい。ここまで来たなら、最後の段階を教えよう。");
    player.gainItem(4031980, -1);
    player.completeQuest(6036);
    var lv = player.getSkillLevel(makerSkill());
    if (lv < 3) {
        player.teachSkill(makerSkill(), 3);
    }
    player.gainExp(300000);
    qm.sendOk("#bメイカー#k は、これで極めたも同然だ。あとはお前次第だな。");
}
