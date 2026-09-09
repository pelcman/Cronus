// メイカースキルの ID は職業系統ごと: 冒険家 1007、シグナス 10001007、アラン 20001007 (Skill.wz 000/1000/2000; Cosmic と同じ計算)。
function makerSkill() {
    return Math.floor(player.getJob() / 1000) * 10000000 + 1007;
}

// マレンの2番目の教え (quest 6033, NPC 2110004 マレン, Lv75) — 中級モンスター結晶 1(4260003)を作って見せると、メイカーを Lv2 に上げてもらえる。
// Cosmic は結晶を自作したかの記録も見るが、Cronus はメイカーの製作を追跡していないので所持だけ確かめる。EXP 230000 は Cosmic の値(JMS の Act は空)。
// 出典 Reference/Cosmic/scripts/quest/6033.js。JMS: 終了スクリプト q6033e、Check は 6029 完了・4260003×1。台詞は創作。
function end() {
    qm.sendNext("ふむ、#b#t4260003##k を持ってきたと言うのか？　どれ、見せてみろ。");
    if (!player.haveItem(4260003)) {
        qm.sendOk("どうした？　モンスター結晶を作ってこいと言ったはずだが。");
        return;
    }
    qm.sendNext("確かに、見事なモンスター結晶だ。これならメイカーの次の段階を教えてもいいだろう。");
    player.completeQuest(6033);
    var lv = player.getSkillLevel(makerSkill());
    if (lv < 2) {
        player.teachSkill(makerSkill(), 2);
    }
    player.gainExp(230000);
    qm.sendOk("#bメイカー#k の腕が上がったはずだ。スキルウィンドウで確かめておけ。");
}
