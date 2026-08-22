// ザクムの祭壇 — 「火の目」を捧げてザクム召喚
// 本体 8800000 は倒すと 8800001 → 8800002 と姿を変える(wz の revive 連鎖)。
// 腕(8800003〜8800010)が残っている間、本体はダメージを受けない(サーバー側ゲート)。
var EYE = 4001017; // 火の目
function start() {
    if (player.mobCount() > 0) {
        cm.sendOk("祭壇はまだ静まっていない……今の戦いが終わるまで待つのだ。");
        return;
    }
    if (!player.haveItem(EYE)) {
        cm.sendOk("ザクムを呼ぶには供物「火の目」が要る。扉の番人から受け取ってくるのだ。");
        return;
    }
    if (!cm.askYesNo("「火の目」を祭壇に捧げれば、火と岩の魔神ザクムが目覚める。"
            + "腕を全て砕くまで本体は無敵だ。……捧げるか?")) {
        cm.sendOk("賢明な判断かもしれんな。");
        return;
    }
    player.gainItem(EYE, -1);
    player.spawnMob(8800000, 1); // 本体 (第1形態)
    for (var arm = 8800003; arm <= 8800010; arm++) {
        player.spawnMob(arm, 1);
    }
    cm.sendOk("大地が震える……ザクムが目覚めた!まずは8本の腕を全て砕け!");
}
