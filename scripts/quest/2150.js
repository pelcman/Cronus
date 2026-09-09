// 噂の真相-イヤン (quest 2150, NPC 1022007 イヤン) — 聞き込みクエスト: 話しかけた時点で完了する。
// 出典 Reference/Cosmic/scripts/quest/2150.js。JMS Check: q2150s、Act1 で EXP 10。台詞は創作。
function start() {
    qm.sendNext("あの木の枝にはマフラーが掛かっているんだ。本当だよ。");
    player.completeQuest(2150);
}
