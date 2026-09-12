// コンペイ 9120203 (アジト前(天晴れ) 801040101, JMS スクリプト名 con4) — ボスを倒した後の帰り道。出典 Reference/Cosmic/scripts/npc/9120203.js。台詞は創作。
function start() {
    cm.sendNext("ああ、ボスが倒されたのか。なんてめでたい日だ！　みんな、おめでとう。こっちから町へ戻れるぞ。");
    player.warp(801000000, 0);
}
