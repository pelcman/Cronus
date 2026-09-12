// 失踪した錬金術師の家の壁(透明) 2111011 (261000001, JMS スクリプト名 absence_wall) — 事件の手がかり(3311)の進行中に調べると、落書きの中に
// 「ペンダント」の形の文句が見つかり、記録 3311 を "5" にする(Cosmic の setQuestProgress)。出典 Reference/Cosmic/scripts/npc/2111011.js。台詞は創作。
function start() {
    if (!player.hasQuest(3311)) {
        return;
    }
    if (!cm.askYesNo("（蜘蛛の巣の奥の壁に、何か書かれているようだ。近づいてよく見てみるか？）")) {
        return;
    }
    player.setQuestData(3311, "5");
    cm.sendOk("（落書きだらけの壁に、ひときわ目立つ一節がある。#bペンダントの形をしている…#k どういう意味だろう？）");
}
