// コンペイ 9120200 (アジト前 801040000, JMS スクリプト名 con2) — ショーワ町 801000000 へ戻る。出典 Reference/Cosmic/scripts/npc/9120200.js。台詞は創作。
function start() {
    if (!cm.askYesNo("ここがアジトの前だ！　なに？　#m801000000# に戻りたいのか？")) {
        cm.sendOk("#m801000000# に戻りたくなったら、俺に言え。");
        return;
    }
    player.warp(801000000, 0);
}
