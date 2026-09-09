// 素公パンダ (So Gong, NPC 2091005) — 武陵道場の案内人。
// 流れは Reference/Cosmic/scripts/npc/2091005.js (GMS v83) を JMS v186 に写したもの。台詞は JMS 原文が
// 手元に無いため創作。サーバー側は Event_DojoAgent を移植した ChannelHandler.Dojo / MuLungDojo。
//   - 挑戦の受付(925020001 武陵道場入口): 一人で挑戦 / ベルト受領 / 修練点数リセット / 勲章[DEV] / 説明
//   - 休憩所(6,12,18,24,30,36 階): 続行 / 退出 / 記録
//   - 戦闘マップ: やめますか? → 出口へ
// ベルト id 1132000〜1132004・必要レベル/点数は v186 String.wz で確認済み。
// パーティー挑戦と＜征服者＞勲章は未実装のため [DEV] を明示する。

var HALL = 925020000, ENTRANCE = 925020001, EXIT = 925020002;
var belts = [1132000, 1132001, 1132002, 1132003, 1132004];
var beltLevel = [25, 35, 45, 60, 75];
var beltPoints = [200, 1800, 4000, 9200, 17000];

function isRestingStage(map) {
    var stage = Math.floor(map / 100) % 100;
    return map >= 925020600 && map <= 925033600 && stage % 6 == 0;
}

function start() {
    var map = player.getMapId();

    if (map == ENTRANCE) {
        entranceMenu();
    } else if (isRestingStage(map)) {
        restingMenu();
    } else {
        // A fighting map: So Gong only appears here when the player wandered back; offer the exit.
        if (cm.askYesNo("どうした、あきらめるのか？　ここでやめて出るか？")) {
            player.warp(EXIT, 0);
        }
    }
}

function entranceMenu() {
    if (player.getLevel() < 25) {
        cm.sendOk("わしの師匠に挑もうというのか？　少なくともレベル #b25#k にはなってから出直してこい。");
        return;
    }

    var sel = cm.askMenu("わしの師匠は武陵で最も強いお方。その師匠に挑もうというのか。\r\n\r\n"
        + "#b#L0#一人で挑戦する#l\r\n"
        + "#L1#仲間と挑戦する#l\r\n"
        + "#L2#ベルトを受け取る#l\r\n"
        + "#L3#修練点数をリセットする#l\r\n"
        + "#L4#勲章を受け取る#l\r\n"
        + "#L5#武陵道場とは？#l#k");

    if (sel == 0) {
        if (!player.dojoEnter(false, 0)) {
            cm.sendOk("道場は今すべて使用中じゃ。しばらく待ってからまた来い。");
        }
    } else if (sel == 1) {
        cm.sendDev("パーティーでの挑戦はまだ用意できていない。今は「一人で挑戦する」を選んでくれ。");
    } else if (sel == 2) {
        beltMenu();
    } else if (sel == 3) {
        if (cm.askYesNo("修練点数をリセットすると 0 に戻るが、よいか？　また一から稼げばベルトをもらい直せるぞ。")) {
            player.setDojoPoints(0);
            cm.sendOk("修練点数をリセットした。新たな気持ちで励むがよい。");
        }
    } else if (sel == 4) {
        cm.sendDev("＜征服者＞の勲章（同じモンスターを100回討伐）はまだ用意できていない。");
    } else if (sel == 5) {
        cm.sendOk("師匠が建てたこの武陵道場は #r38階#k の塔じゃ。一階ごとに己を鍛えられる。お前の実力で頂上まで行けるかな？");
    }
}

function beltMenu() {
    var pts = player.dojoPoints();
    var text = "お前の修練点数は #b" + pts + "#k 点。実力に応じてベルトを授けよう。\r\n";
    for (var i = 0; i < belts.length; i++) {
        text += "\r\n#L" + i + "##i" + belts[i] + "# #t" + belts[i] + "#" + (player.haveItem(belts[i]) ? "（所持済み）" : "");
    }
    var i = cm.askMenu(text);
    var belt = belts[i], level = beltLevel[i], need = beltPoints[i];
    var prev = i > 0 ? belts[i - 1] : -1;

    if (player.haveItem(belt)) {
        cm.sendOk("その #t" + belt + "# はもう授けておるぞ。");
        return;
    }
    if (prev != -1 && !player.haveItem(prev)) {
        cm.sendOk("#i" + prev + "# #t" + prev + "# を持っていないとその上のベルトは授けられぬ。");
        return;
    }
    if (player.getLevel() <= level) {
        cm.sendOk("そのベルトにはレベル #b" + level + "#k を超えていることが必要じゃ。");
        return;
    }
    if (pts < need) {
        cm.sendOk("そのベルトには #b" + need + "#k 点の修練点数が要る。あと #r" + (need - pts) + "#k 点じゃ。");
        return;
    }
    if (prev != -1) {
        player.gainItem(prev, -1);
    }
    player.gainItem(belt, 1);
    player.setDojoPoints(pts - need);
    cm.sendOk("これが #i" + belt + "# #b#t" + belt + "##k じゃ。よく励んだな！");
}

function restingMenu() {
    var stage = Math.floor(player.getMapId() / 100) % 100;
    var sel = cm.askMenu("ここまでよく来た！　だがここからは楽ではないぞ。まだ挑むか？\r\n\r\n"
        + "#b#L0#続けて挑戦する#l\r\n"
        + "#L1#ここでやめる#l\r\n"
        + "#L2#ここまでの記録を残す#l#k");

    if (sel == 0) {
        if (!player.dojoEnter(false, stage)) {
            cm.sendOk("道場は今すべて使用中じゃ。しばらく待ってからまた来い。");
        }
    } else if (sel == 1) {
        if (cm.askYesNo("本当にここでやめるのか？")) {
            player.warp(EXIT, 0);
        }
    } else if (sel == 2) {
        cm.sendDev("記録機能（次回このフロアから再開）はまだ用意できていない。今はそのまま続けて挑戦してくれ。");
    }
}
