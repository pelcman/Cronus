// ブロックパスは外界生物？ (quest 3452, NPC 2050001 ドクター中村, ジパング) — ブロックパスのストラップ(4000099)を届けると完了。
// JMS: 終了スクリプト q3452e、Check[1] は 4000099×1、Act は空なので報酬(マナエリクサー丸薬 2000011×50、EXP 8000 = Cosmic の値)は
// スクリプト側。台詞は創作。
function end() {
    if (!player.haveItem(4000099)) {
        qm.sendOk("#b#t4000099##k はまだかね？　あれがないと研究が進まないのだよ。");
        return;
    }
    qm.sendNext("これが #b#t4000099##k か…実に興味深い。感謝の印に、この #b#t2000011##k を受け取ってくれたまえ。");
    player.gainItem(4000099, -1);
    player.gainItem(2000011, 50);
    player.gainExp(8000);
    player.completeQuest(3452);
}
