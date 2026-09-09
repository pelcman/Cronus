// tutorialNPC — 職業の殿堂入口のポータル: レベル10以下の初心者(job 0)には転職教官の会話を開く。それ以外は何もしない。
// 出典 Reference/Cosmic/scripts/portal/tutorialNPC.js。教官 NPC(航海室 カイリン 1090000 / 戦士の聖殿 1022000 /
// 盗賊のアジト ダークロード 1052001 / 弓使い学院 ヘレナ 1012100 / 魔法図書館 ハインズ 1032001)は JMS v186 に画像と
// スクリプトがあることを確認済み。
var INSTRUCTOR = { 120000101: 1090000, 102000003: 1022000, 103000003: 1052001, 100000201: 1012100, 101000003: 1032001 };
function start() {
    if (player.getLevel() > 10 || player.getJob() != 0) {
        return;
    }
    var npc = INSTRUCTOR[player.getMapId()];
    if (npc) {
        player.openNpc(npc);
    }
}
