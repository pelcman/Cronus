// 失踪した錬金術師の家の机(透明) 2111014 (261000001, JMS スクリプト名 absence_desk) — 事件の手がかり(3311)の進行中に調べると、ドラン博士の日記が読める。
// 出典 Reference/Cosmic/scripts/npc/2111014.js。台詞は創作。
function start() {
    if (!player.hasQuest(3311)) {
        return;
    }
    cm.sendOk("（ドラン博士の日記だ。どのページも数式と、もったいぶった科学の文章で埋め尽くされている。）");
}
