// ドラゴン (NPC 1013001) — 夢の中「契約の条件」への案内。話しかけると 900090101 へ送る。
// 出典 Reference/Cosmic/scripts/npc/1013001.js を JMS v186 に移植。台詞は創作、行き先は JMS の該当マップで確認済み。
function start() {
    cm.sendNext("ドラゴンマスターとなる運命の者よ…ついに、ここへ辿り着いたか。");
    cm.sendNext("行くがよい。ドラゴンマスターとしての務めを果たすのだ…");
    player.warp(900090101, 0);
}
