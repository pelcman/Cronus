// 砂漠へ… (quest 2127, 完了 NPC 1022002 マンジ, Lv18〜29) — 説明を聞いて完了。出典 Reference/Cosmic/scripts/quest/2127.js。
// JMS Check: 終了スクリプト q2127e。台詞は創作。
function end() {
    qm.sendOk("準備はできているようだな。では、任務の詳細をよく聞いてくれ…");
    player.completeQuest(2127);
}
