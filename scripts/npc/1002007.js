// メイプル運輸タクシー — ビクトリアアイランドの町へ移動
function start() {
    var towns = [
        ["ヘネシス", 100000000, 1000],
        ["エリニア", 101000000, 1000],
        ["ペリオン", 102000000, 1000],
        ["カニングシティ", 103000000, 1000],
        ["リス港", 104000000, 800],
        ["ノーチラス", 120000000, 1000]
    ];
    var menu = "どこへ行きますか?料金は前払いです。";
    for (var i = 0; i < towns.length; i++) {
        menu += "\r\n#L" + i + "#" + towns[i][0] + " (" + towns[i][2] + "メル)#l";
    }
    var pick = cm.askMenu(menu);
    var town = towns[pick];
    if (player.getMapId() == town[1]) {
        cm.sendOk("もうここにいますよ。");
        return;
    }
    if (player.getMeso() < town[2]) {
        cm.sendOk("メルが足りないようですね。");
        return;
    }
    player.gainMeso(-town[2]);
    player.warp(town[1]);
}
