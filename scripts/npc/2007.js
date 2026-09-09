// イベントガイド (Event Guide, NPC 2007) — チュートリアルを飛ばしてリスハーバー(104000000)へ送る。
// 出典 Reference/Cosmic/scripts/npc/2007.js を JMS v186 に移植。台詞は創作、行き先はJMSのリス/港口で確認済み。
function start() {
    if (cm.askYesNo("チュートリアルを飛ばして、リスハーバーへ直接向かいますか？")) {
        player.warp(104000000, 0);
    } else {
        cm.sendOk("よい旅を。");
    }
}
