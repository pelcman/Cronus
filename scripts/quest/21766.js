// 怪しいゾーン (quest 21766, NPC 1002001 テオ, リス港, Lv13) — 関節炎の愚痴をやめたゾーンの変化と、彼の木箱の秘密を探る。受注は説明の後、完了は 木箱の秘密(21767)
// を終えてから。EXP 200 は Cosmic の値(JMS の Act は空)。出典 Reference/Cosmic/scripts/quest/21766.js。JMS: 開始 q21766s / 終了 q21766e、Check は 21706 進行中。台詞は創作。
function start() {
    qm.sendNext("おい！　頼みがあるんだ。#p20000# の様子が、最近ちょっとおかしいんだよ。");
    qm.sendNext("あいつ、ついこの間まで関節炎だなんだと文句ばかり言っていたのに、急に静かになった。");
    qm.sendNext("あの木箱に、何か秘密がある気がするんだ。");
    qm.sendNext("#p20000# の居場所は分かるだろう？　右の方だ。あいつの木箱を調べてみてくれ。");
    player.startQuest(21766);
}

function end() {
    qm.sendNext("なるほど、箱の中身は薬だったのか。それで急に元気になったってわけだ。ありがとうな、これで安心した。");
    player.completeQuest(21766);
    player.gainExp(200);
}
