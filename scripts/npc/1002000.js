// ピル (リス港, wz script = rithTeleport) — ビクトリア各町への移動サービス。
// 本来の演出のオラクルが無いため、タクシー同等の有料ワープとして実装。
function start() {
    var towns = [
        ["ヘネシス", 100000000, 1000],
        ["エリニア", 101000000, 1000],
        ["ペリオン", 102000000, 1000],
        ["カニングシティ", 103000000, 1000],
        ["ノーチラス", 120000000, 800]
    ];
    var menu = "リス港からどこへ向かうんだい?料金は前払いだよ。";
    for (var i = 0; i < towns.length; i++) {
        menu += "\r\n#L" + i + "#" + towns[i][0] + " (" + towns[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= towns.length) return;
    var town = towns[pick];
    if (player.getMapId() == town[1]) {
        cm.sendOk("もうそこにいるじゃないか。");
        return;
    }
    if (player.getMeso() < town[2]) {
        cm.sendOk("メルが足りないようだね。" + town[2] + "メル必要だよ。");
        return;
    }
    player.gainMeso(-town[2]);
    player.warp(town[1]);
}
