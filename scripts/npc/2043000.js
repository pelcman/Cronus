// ビシャスプラント 2043000 (時計塔の深層部 922020300, JMS スクリプト名 s4time) — この世界の者ではない、と 時計塔の奥 220080000 へ送り返す。
// 出典 Reference/Cosmic/scripts/npc/2043000.js。台詞は創作。
function start() {
    cm.sendNext("お前はこの世界の者ではない…今すぐ戻れ。");
    player.warp(220080000, 0);
}
