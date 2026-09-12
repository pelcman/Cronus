// 失踪した錬金術師の家の額縁(透明) 2111013 (261000001, JMS スクリプト名 absence_frame) — フィリアのペンダント(3322)の進行中に調べると、額縁の裏から
// 銀のペンダント(4031697) が見つかる(一度だけ)。出典 Reference/Cosmic/scripts/npc/2111013.js。台詞は創作。
function start() {
    if (!player.hasQuest(3322) || player.haveItem(4031697)) {
        return;
    }
    cm.sendOk("（額縁の裏の留め具を外すと、中に隠し空間があった。銀のペンダントが入っている。そっと取り出して、額縁を元どおり机の上に戻した。）");
    player.gainItem(4031697, 1);
}
