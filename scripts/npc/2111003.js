// ヒュモノイドA 2111003 (マガティア 261000000, JMS スクリプト名 snow_rose) — 雪園バラとその謂れ(3335)の進行中で 雪園バラ(4031695) をまだ持っていなければ
// 雪園バラが育つ地 926120300 へ送る。それ以外は一言。出典 Reference/Cosmic/scripts/npc/2111003.js。台詞は創作。
function start() {
    if (player.hasQuest(3335) && !player.haveItem(4031695)) {
        player.warpPortal(926120300, "out00");
        return;
    }
    cm.sendOk("私が感じているこの感情は本物なのか？　それとも、機械の誤作動が生む幻なのか…？");
}
