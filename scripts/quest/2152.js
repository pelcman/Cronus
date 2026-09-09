// 噂の真相-Dr.ベティ (quest 2152, NPC 1032104 Dr.ベティ) — 聞き込みクエスト: 話しかけた時点で完了する。
// 出典 Reference/Cosmic/scripts/quest/2152.js。JMS Check: q2152s、Act1 で EXP 15。台詞は創作。
function start() {
    qm.sendNext("あの木…聞いたことがあるわ。研究したこともある！　#bスタンピ#kは、何らかの魔力で土地が痩せたときに動き出すの。"
        + "そうやって生まれた切り株は、水や養分の代わりに怪しい魔力を吸って生きるから、近くの人や村にとってとても危険なのよ。");
    player.completeQuest(2152);
}
