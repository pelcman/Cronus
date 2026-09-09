// カルタサの告白 (quest 2257, 完了 NPC 2110005 ラクダ) — カルタサの頼みでラクダに話すと完了。出典 Reference/Cosmic/scripts/quest/2257.js。
// JMS: 終了スクリプト q2257e、報酬なし。台詞は創作。
function end() {
    qm.sendNext("やあ、#r#m261000000##k まで乗っていくかい？　おや、#b#p2101013##k からの頼みか？");
    player.completeQuest(2257);
}
