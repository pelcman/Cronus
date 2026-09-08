// 林次長 (カニングシティ 捨てられた地下鉄の歴史 910320000, wz script = subway) — 地下鉄「殲滅」ミニゲームの入口。
// 流れは Reference/Cosmic/scripts/npc/1052115.js(GMS v83)を JMS v186 に写したもの。台詞は JMS 原文が
// 手元に無いため創作。サーバー側は JMSv186 oracle の Event_PyramidSubway を移植した MassacreEvent
// (ホコリだらけのプラットフォーム 910320100〜 3 ステージ、999番の客車 910320010〜 ボーナス、結果 910330001)。
// 勲章 1142141 / クエスト 29931 / 記録 7662 は Cosmic どおり(v186 データでの存在は要確認)。
var PASS = 4001321;      // 999番の客車 乗車券
var MEDAL = 1142141;     // ＜名誉職員＞の勲章
var MEDAL_QUEST = 29931;
var RECORD = 7662;       // 討伐数の記録(クエスト記録の customData)

function start() {
    var map = player.getMapId();
    if (map == 910320001) {
        player.warp(910320000, 0);
        return;
    }
    if (map == 910330001) {
        player.gainItem(PASS, 1);
        player.warp(910320000, 0);
        return;
    }
    if (map >= 910320100 && map <= 910320304) {
        if (cm.askYesNo("ここから出ますか？")) {
            player.warp(910320000, 0);
        }
        return;
    }

    var sel = cm.askMenu("私は林次長です。捨てられた地下鉄の見回りをしています。\r\n"
        + "#b#L1#ホコリだらけのプラットフォームに入る#l\r\n"
        + "#L2#999番の客車へ向かう#l\r\n"
        + "#L3#＜名誉職員＞の勲章を受け取る#l#k");
    if (sel == 1) {
        if (player.getLevel() < 25 || player.getLevel() > 30 || !player.isPartyLeader()) {
            cm.sendOk("プラットフォームに入れるのは、#bレベル25〜30#kの#bパーティーリーダー#kだけです。");
            return;
        }
        if (!player.startSubwayMassacre()) {
            cm.sendOk("ホコリだらけのプラットフォームは今、満員です。しばらくしてからまた来てください。");
        }
    } else if (sel == 2) {
        if (!player.haveItem(PASS)) {
            cm.sendOk("#b#t" + PASS + "##kをお持ちでないようですね。プラットフォームを最後まで走り抜けた人にだけ渡しています。");
            return;
        }
        if (player.bonusSubwayMassacre()) {
            player.gainItem(PASS, -1);
        } else {
            cm.sendOk("999番の客車は今、満員です。");
        }
    } else if (sel == 3) {
        var data = player.getQuestData(RECORD);
        var mons = data == null || data == "" ? 0 : parseInt(data);
        if (mons < 10000) {
            cm.sendOk("駅のモンスターを#b10,000匹#k以上倒してから、また私を探してください。\r\n現在の討伐数: #r" + mons + "#k");
        } else if (player.haveItem(MEDAL)) {
            cm.sendOk("勲章はもうお渡ししていますよ。");
        } else {
            player.gainItem(MEDAL, 1);
            player.startQuest(MEDAL_QUEST);
            player.completeQuest(MEDAL_QUEST);
            cm.sendOk("お疲れさまでした。＜名誉職員＞の勲章をお受け取りください。");
        }
    }
}
