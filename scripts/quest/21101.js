// 鉾の使い手たる英雄 (quest 21101, NPC 1201001 巨大な鉾, リエン) — 鉾に触れて自分が英雄アランだと確かめ、レジェンド(2000)からアラン(2100)へ 1次転職。
// ステータスは職業の初期値に戻して残りは AP(Cosmic の resetStats)。勲章 目覚めたアランの勲章(1142129) は JMS では 29924 の称号クエストが渡す。
// 出典 Reference/Cosmic/scripts/quest/21101.js。JMS: 開始 q21101s / 終了 q21101e、Check は 21100 完了・Lv10、Act は空。台詞は創作。
function start() {
    if (!qm.askYesNo("（本当に自分が、この #p1201001# を振るった英雄だったのだろうか…？　確かめてみるか？）")) {
        qm.sendOk("（少し考える時間が必要だ…。）");
        return;
    }
    if (player.getJob() != 2000) {
        qm.sendOk("（鉾はもう静かに眠っている。力はすでに、この手に戻っている。）");
        return;
    }
    player.changeJob(2100);
    player.resetStatsForJob();
    player.startQuest(21101);
    player.completeQuest(21101);
    qm.sendOk("（鉾に触れた瞬間、体の奥で何かが目を覚ました…。何かを思い出しかけている…。）");
}

function end() {
    if (player.getJob() < 2100) {
        qm.sendOk("（まだ鉾は応えない…。）");
        return;
    }
    player.completeQuest(21101);
}
