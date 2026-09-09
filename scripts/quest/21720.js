function remember(skillId, name) {
    if (player.getSkillLevel(skillId) < 1) {
        player.teachSkill(skillId, 1);
    }
    qm.sendDev("（#b" + name + "#k のスキルを思い出した！　スキルウィンドウで確かめよう。）");
}

// 人形使いの警告 (quest 21720, 受注 NPC 1002104 トゥルー → 完了 NPC 1201000 リリン, Lv22) — 人形使いとブラックウイングのことをリリンに伝える。
// 完了でブースター(鉾)(21001003)を思い出す(Lv1 を渡す [DEV])。EXP 3900 は Cosmic の値(JMS の Act は空)。出典 Reference/Cosmic/scripts/quest/21720.js。
// JMS: 終了スクリプト q21720e、受注は データ経路(Check は 21719 完了 + 隠し記録 21760 = "0")。台詞は創作。
function end() {
    qm.sendNext("どうしました？　トゥルーから、あなたが何か大事な話を持ってくると連絡がありましたが…。");
    qm.sendNext("（人形使いとブラックウイングのことを伝える。）");
    qm.sendNext("そうですか…ブラックウイングという集団がいるとは知りませんでした。暗黒の魔法使いの復活を企む者たち…。");
    qm.sendNext("（…それは本当のことだ。彼女はまったく怖がっていないように見えるが…。）");
    qm.sendNext("預言書には、英雄は暗黒の魔法使いが目覚めるときによみがえる、とあります。つまり、その時は近い。");
    qm.sendNext("（怖くないのか？）");
    if (!qm.askYesNo("怖い？　ふふ。暗黒の魔法使いが現れたって構いません。あなたがいるんですから。さあ、次の修行に進みましょう。いいですね？")) {
        qm.sendOk("…そうですね。少し休んでから、また来てください。");
        return;
    }
    player.completeQuest(21720);
    player.gainExp(3900);
    remember(21001003, "ブースター(鉾)");
    qm.sendNext("この技は、古い解読不能の巻物に記されていたものです。あなたの体が、勝手に覚えていたのでしょう。");
    qm.sendNext("あなたは着実に強くなっています。私はいつでもここで、あなたを支えます。");
    qm.sendOk("そのためにできることは一つ。修行、修行、また修行です！");
}
