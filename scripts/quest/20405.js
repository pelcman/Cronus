// 暗黒の魔女の洞窟 (quest 20405, NPC 2081013 禍々しい玉) — 洞窟の壁のメモを読む。読むとその場で受注→完了。
// 出典 Reference/Cosmic/scripts/quest/20405.js。JMS: 開始スクリプト q20405s、Check は 20404 完了・Lv120、Act は空。台詞は創作。
function start() {
    qm.sendNext("（壁にメモが貼られている。『呪いの源はまだここで脈打っている。装置は鑑定のために #rエレヴ#k へ送った…』）");
    qm.sendNext("（『…私は先へ進む。もし戻れなければ、この記録を宰相へ。』——上級騎士の筆跡だ。）");
    player.startQuest(20405);
    player.completeQuest(20405);
}
