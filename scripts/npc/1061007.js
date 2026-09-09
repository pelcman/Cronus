// 崩れている石像 (Crumbling Statue, NPC 1061007) — 忍耐の森「1〜3段階」(105040310〜312)からスリーピーウッド(105040300)へ出る。
// 出典 Reference/Cosmic/scripts/npc/1061007.js を JMS v186 に移植。行き先は JMS に実在確認済み。台詞は創作。
function start() {
    if (cm.askYesNo("ここから出ますか？")) {
        player.warp(105040300, 0);
    }
}
