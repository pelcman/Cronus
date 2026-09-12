// 母牛 1092090 (ノーチラスの牛小屋 912000100, JMS スクリプト名 mom_cow) — 空のミルクボトル(4031847)を 1/3(4031848) → 2/3(4031849) → 満タン(4031850) と満たしていく。
// 同じ牛から続けては搾れない(直前に搾った牛を 新鮮な牛乳を手に入れる(2180) の記録 "1" で覚える)。出典 Reference/Cosmic/scripts/npc/1092090.js。台詞は創作。
function start() {
    if (player.getQuestData(2180) == "1") {
        cm.sendOk("この牛からはさっき搾ったばかりだ。別の牛を当たろう。");
        return;
    }
    if (player.haveItem(4031847)) {
        cm.sendOk("ボトルに牛乳を注いでいく…。ボトルは 3 分の 1 まで満たされた。");
        player.gainItem(4031847, -1);
        player.gainItem(4031848, 1);
    } else if (player.haveItem(4031848)) {
        cm.sendOk("ボトルに牛乳を注いでいく…。ボトルは 3 分の 2 まで満たされた。");
        player.gainItem(4031848, -1);
        player.gainItem(4031849, 1);
    } else if (player.haveItem(4031849)) {
        cm.sendOk("ボトルに牛乳を注いでいく…。ボトルは満タンになった。");
        player.gainItem(4031849, -1);
        player.gainItem(4031850, 1);
    } else {
        cm.sendOk("牛乳を入れるボトルが無い。");
        return;
    }
    player.setQuestData(2180, "1");
}
