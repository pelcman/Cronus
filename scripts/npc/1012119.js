// ヘンケル 1012119 (東の丘 100010000, JMS スクリプト名 enter_archer) — Lv20 未満の初心者を 弓使い修行場 910060000 へ送る。ヘンケルの修練場〜妄想(22515〜22518)の
// 進行中は ヘンケルのスポア修練場 910060100 へ。JMS の 910060001〜004 は中身の無い複製枠なので、1 部屋(定員なし)で送る [DEV]。
// 出典 Reference/Cosmic/scripts/npc/1012119.js。台詞は創作。
function start() {
    if (player.getLevel() >= 20) {
        cm.sendOk("この修行場は、レベル 20 未満の人だけが使えるんだ。");
        return;
    }
    if (player.hasQuest(22515) || player.hasQuest(22516) || player.hasQuest(22517) || player.hasQuest(22518)) {
        if (cm.askYesNo("特別修練場に入るかい？")) {
            player.warp(910060100, 0);
        }
        return;
    }
    if (cm.askYesNo("[DEV] 修行場に入るかい？")) {
        player.warp(910060000, 0);
    }
}
