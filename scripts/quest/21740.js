function remember(skillId, name) {
    if (player.getSkillLevel(skillId) < 1) {
        player.teachSkill(skillId, 1);
    }
    qm.sendDev("（#b" + name + "#k のスキルを思い出した！　スキルウィンドウで確かめよう。）");
}

// 奪われたオルビスの封印石 (quest 21740, 受注 NPC 1002104 トゥルー → 完了 NPC 1201000 リリン, Lv45) — オルビスの封印石が奪われたことをリリンへ。
// 完了でコンボスマッシュ(21100004)を思い出す(Lv1 [DEV])。出典 Reference/Cosmic/scripts/quest/21740.js。JMS: 開始 q21740s / 終了 q21740e、Act は空。台詞は創作。
function start() {
    qm.sendNext("オルビスの封印石が、ブラックウイングに奪われた？　ふむ…すぐにリリンに知らせてくれ。");
    player.startQuest(21740);
}

function end() {
    qm.sendNext("あ、#h0#！　聞いてください、さっき分かったことがあるんです。封印石の力は、英雄の技と深く結びついているようなのです。");
    qm.sendNext("ひとまず、あなたの体が思い出した技を確かめておきましょう。");
    player.completeQuest(21740);
    remember(21100004, "コンボスマッシュ");
}
