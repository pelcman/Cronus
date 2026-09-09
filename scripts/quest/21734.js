function remember(skillId, name) {
    if (player.getSkillLevel(skillId) < 1) {
        player.teachSkill(skillId, 1);
    }
    qm.sendDev("（#b" + name + "#k のスキルを思い出した！　スキルウィンドウで確かめよう。）");
}

// フランシスの痕跡 (quest 21734, NPC 1002104 トゥルー, Lv37) — 人形使いフランシス(9300346)を倒して報告。完了でコンボドレイン(21100005)を思い出す(Lv1 [DEV])。
// EXP 12500 は Cosmic の値(JMS の Act は空)。出典 Reference/Cosmic/scripts/quest/21734.js。JMS: 開始 q21734s / 終了 q21734e、normalAutoStart、完了は 9300346×1。台詞は創作。
function start() {
    qm.sendNext("やあ、アラン。人形使いがまた動き出したという報告が入った。今度こそ、やつの尻尾をつかんでくれ。");
    player.startQuest(21734);
}

function end() {
    qm.sendNext("やったな、アラン！　これで人形使いは、もうこの島を荒らせない。");
    qm.sendNext("やつらは #bビクトリアアイランドの封印石#k を狙っていた。封印石は、暗黒の魔法使いを封じる要だ。");
    qm.sendNext("一連の任務での勇敢な働きに感謝する。それと…お前の体が、また一つ技を思い出したようだ。");
    player.completeQuest(21734);
    player.gainExp(12500);
    remember(21100005, "コンボドレイン");
}
