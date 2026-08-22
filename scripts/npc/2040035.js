// アルト — LPQ最終ステージ: アリシャー召喚とクリア判定 (簡略版)
var KEY = 4001023; // アリシャーの鍵
function start() {
    if (player.haveItem(KEY)) {
        player.gainItem(KEY, -1);
        player.gainExp(8000);
        var pick = cm.askMenu("アリシャーを倒したのね!エオス塔を救ってくれてありがとう!"
            + "\r\n#L0#ボーナスステージへ#l"
            + "\r\n#L1#エオス塔101階へ帰る#l");
        player.warp(pick == 0 ? 922011000 : 221024500);
        return;
    }
    if (player.mobCount() > 0) {
        cm.sendOk("アリシャーとの戦いの最中よ!倒して鍵を手に入れて!");
        return;
    }
    if (cm.askYesNo("この奥に潜む魔法生物アリシャーを呼び出すわ。準備はいい?")) {
        player.spawnMob(9300012, 1);
        cm.sendOk("来るわよ……アリシャーを倒して、落とす鍵を私に見せて!");
    }
}
