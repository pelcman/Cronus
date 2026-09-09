// ジェームスの行方1 (quest 2325, 受注 1300005 警護大将 → 完了 NPC 1300008 ジェイムズ, 中央の城塔 106021201) — 弟のジェイムズを見つけて
// 話しかけると完了。出典 Reference/Cosmic/scripts/quest/2325.js。JMS: 終了スクリプト q2325e、Act1 で EXP 6000 と次のクエスト 2326
// (完了処理で付与)。台詞は創作。
function end() {
    qm.sendNext("こ…怖い…お願い…助けて…");
    qm.sendNext("怖がらないで。#b#p1300005##k に頼まれて来たんだ。");
    player.completeQuest(2325);
    qm.sendOk("え？　兄さんが？　ああ…これで助かった。本当にありがとう…");
}
