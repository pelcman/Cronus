function remember(skillId, name) {
    if (player.getSkillLevel(skillId) < 1) {
        player.teachSkill(skillId, 1);
    }
    qm.sendDev("（#b" + name + "#k のスキルを思い出した！　スキルウィンドウで確かめよう。）");
}

// 奪われた武陵の封印石 (quest 21748, 受注 NPC 1002104 トゥルー → 完了 NPC 1201000 リリン, Lv54) — 武陵の封印石の件をリリンへ報告。完了でファイナルチャージ(21100002)を
// 思い出す(Lv1 [DEV])。EXP 20000 は Cosmic の値(JMS の Act は空)。出典 Reference/Cosmic/scripts/quest/21748.js。JMS: 終了スクリプト q21748e。台詞は創作。
function end() {
    qm.sendNext("アラン、無事に帰ってきましたね！　任務はどうでしたか？");
    qm.sendNext("私は、失われた英雄の技の手がかりを探して、古い技術書を調べていました。あなたの体は、また一つ思い出したようです。");
    player.completeQuest(21748);
    player.gainExp(20000);
    remember(21100002, "ファイナルチャージ");
}
