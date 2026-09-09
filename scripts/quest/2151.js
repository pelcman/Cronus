// 噂の真相-コブシを開いて立て (quest 2151, NPC 1022000) — 聞き込みクエスト: 話しかけた時点で完了する。
// 出典 Reference/Cosmic/scripts/quest/2151.js。JMS Check: q2151s、Act1 で EXP 10。台詞は創作。
function start() {
    qm.sendNext("あの木には、恐ろしい顔のような妙な彫り込みがあるんだ。");
    player.completeQuest(2151);
}
