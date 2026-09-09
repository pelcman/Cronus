// タイラス (Tylus, NPC 2022004) — 修行を終えてエルナス(211000000, ポータル in01)へ戻す。
// 出典 Reference/Cosmic/scripts/npc/2022004.js を JMS v186 に移植。台詞は創作、行き先・ポータル in01 はJMSで確認済み。
function start() {
    cm.sendNext("見事だったな、" + player.getName() + "。エルナスへ送り届けよう。ペンダントを持って、準備ができたらまた話しかけてくれ。新しいスキルを授けよう。");
    player.warpPortal(211000000, "in01");
}
