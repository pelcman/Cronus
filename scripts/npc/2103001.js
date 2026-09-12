// 秘密の壁 2103001 (アリアント集落地 260000200, JMS スクリプト名 secret_wall) — 秘密組織の存在(3927)の進行中に調べると合言葉が聞こえ、記録 3927 に印を付ける
// (Cosmic の setQuestProgress(3927, 1))。出典 Reference/Cosmic/scripts/npc/2103001.js。台詞は創作。
function start() {
    if (!player.hasQuest(3927)) {
        cm.sendOk("（古びた壁だ。何かの隠し扉のようにも見える…。）");
        return;
    }
    cm.sendNext("（壁の向こうから声がする…「鉄の槌と短剣、弓と矢があれば…」）");
    player.setQuestData(3927, "1");
}
