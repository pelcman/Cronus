// 噂の真相-豚と一緒に踊りを (quest 2148, NPC 1020000) — 聞き込みクエスト: 話しかけた時点で完了する。
// 出典 Reference/Cosmic/scripts/quest/2148.js。JMS Check: 開始スクリプト q2148s、Act1 で EXP 10(完了処理で付与)。台詞は創作。
function start() {
    qm.sendNext("あの木のまわりを、いつもコウモリが飛び回っているらしい…気味が悪いよ。");
    player.completeQuest(2148);
}
