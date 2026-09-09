// ホーンテイルが残したもの…。 (quest 3714, NPC 2081011 ナインスピリットの子龍, リプレ) — ナインスピリットの卵(4001094)を巣に返すと
// ホーンテイルの心臓(2041200)と EXP 42000(Cosmic の値; JMS の Act は空)。繰り返し可(interval 0)。出典 Reference/Cosmic/scripts/quest/3714.js。
// JMS: 開始スクリプト q3714s、Check は 3706 完了と卵の所持。台詞は創作。
function start() {
    if (!player.haveItem(4001094)) {
        qm.sendNext("#b#t4001094##k を持っていないようだね…。");
        if (player.haveItem(2041200)) {
            qm.sendOk("（持っている #b#t2041200##k が、ここに来てから一段と明るく光っている…。）");
        }
        return;
    }
    qm.sendNext("#b#t4001094##k を持ってきてくれたんだね。同族をまた一つ巣に返してくれてありがとう。お礼に、これを受け取って。");
    player.gainItem(4001094, -1);
    player.gainItem(2041200, 1);
    player.gainExp(42000);
    player.completeQuest(3714);
}
