// 噂の真相-マンジ (quest 2149, NPC 1022002 マンジ) — 聞き込みクエスト: 話しかけた時点で完了する。
// 出典 Reference/Cosmic/scripts/quest/2149.js。JMS Check: q2149s、Act1 で EXP 10。台詞は創作。
function start() {
    qm.sendNext("この土地に何か不吉なことが起きるたび、古い木が動き出すと言われている…あの化け物から村を守る英雄が必要だ！");
    player.completeQuest(2149);
}
