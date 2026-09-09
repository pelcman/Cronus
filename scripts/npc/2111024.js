// 秘密通路(透明) 2111024 (JMS スクリプト名 secretNPC, 研究所1階廊下 261010000 / 研究所B-1区域 261020200 の扉) — 暗証番号の認証(3360)で
// 教わったマスターキーを入力すると認証され(記録 3360 を "1" に)、暗黒の魔法使いの研究室へ続く道 261030000 へ通す。認証済み・3360 完了後は
// そのまま通す。出典 Reference/Cosmic/scripts/npc/MagatiaPassword.js + portal/secretDoor.js。台詞は創作。
function cross() {
    player.warpPortal(261030000, player.getMapId() == 261010000 ? "sp_jenu" : "sp_alca");
}

function start() {
    if (player.isQuestDone(3360) || player.getQuestData(3360) == "1") {
        cross();
        return;
    }
    if (!player.hasQuest(3360)) {
        cm.sendOk("（扉は固く閉ざされている。何かの認証装置が付いているようだ…。）");
        return;
    }
    var key = player.getQuestData(3360);
    var answer = cm.askText("扉が差し込まれた通行証に反応した。#bパスワード#k を入力せよ！");
    if (answer == key) {
        player.setQuestData(3360, "1");
        cm.sendOk("（認証された。扉がゆっくりと開いていく…。）");
        cross();
        return;
    }
    cm.sendOk("#r違う！#k");
}
