// 記憶する人 (quest 21750, 受注 NPC 2131002 ユリス → 完了 NPC 2131000 ヘレナ, Lv63, エリン森) — 過去のヘレナに会う。話すと完了。
// 出典 Reference/Cosmic/scripts/quest/21750.js。JMS: 終了スクリプト q21750e(受注は fieldEnter で自動)。台詞は創作。
function end() {
    qm.sendNext("アラン、やっと帰ってきたのね！！　今までどこにいたの？　みんな、あなたを待っていたのよ…。");
    player.completeQuest(21750);
}
