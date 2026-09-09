// リッチの感謝 (quest 2228, NPC 1032108 明るいリッフ) — パウストを倒した礼。話した時点で完了(JMS Act1 で人気度 +8、完了処理で付与)。
// 出典 Reference/Cosmic/scripts/quest/2228.js。JMS: 開始スクリプト q2228s。台詞は創作。
function start() {
    qm.sendNext("#rパウスト#k を倒してくれてありがとう。これでやっと、私の魂も安らかに眠れる。");
    player.completeQuest(2228);
}
