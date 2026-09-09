// 不思議な石像 (Mysterious Statue, NPC 1063016) — 試練から出る。105040201(人形使いの隠れ家)のポータル2へ。
// 出典 Reference/Cosmic/scripts/npc/1063016.js を JMS v186 に移植。台詞は創作、行き先はJMSで確認済み。
function start() {
    if (cm.askYesNo("この試練から出ますか？")) {
        player.warp(105040201, 2);
    }
}
