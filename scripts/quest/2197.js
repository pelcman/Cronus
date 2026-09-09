// モンスターブックのティエンク (quest 2197, NPC 2006 ティエンク, リス港) — Lv10 以下の初心者がリス港(104000000)に入ると自動で始まる
// モンスターブックの案内。ティエンクに話すと完了。JMS: 終了スクリプト q2197e、Act は空。出典 Reference/Cosmic/scripts/quest/2197.js。台詞は創作。
function end() {
    qm.sendNext("おや、もう #bモンスターブック#k を持っているんだね。倒したモンスターのカードを集めて図鑑を埋めていくといい。良い旅を！");
    player.completeQuest(2197);
}
