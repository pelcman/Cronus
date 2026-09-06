// ピル (リス港, wz script = rithTeleport) — ビクトリアアイランド各町へのタクシー(リス港を除く)。
// 台詞は JMS 原文(Riremito/jms_scripts より)。料金はオラクルに値が無いため簡易(創作):
// 1000メル、初心者(職業0)は1/10。行き先は6町に限定(選択肢IDはエンジン側で検証される)。
var TOWNS = [
    ["ヘネシス", 100000000, 1000],
    ["エリニア", 101000000, 1000],
    ["ペリオン", 102000000, 1000],
    ["カニングシティー", 103000000, 1000],
    ["リス港", 104000000, 800],
    ["ノーチラス", 120000000, 1000]
];
function fare(base) {
    return player.getJob() == 0 ? Math.floor(base / 10) : base;
}
function start() {
    var here = player.getMapId();
    var menu = "君は初心者ではないな？なら料金は規定どおりにいただくぜ？さあ、どの村へ行きたいんだい？";
    for (var i = 0; i < TOWNS.length; i++) {
        if (TOWNS[i][1] == here) continue;
        menu += "\r\n#L" + i + "##b" + TOWNS[i][0] + " (" + fare(TOWNS[i][2]) + "メル)#k#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= TOWNS.length || TOWNS[pick][1] == here) return;
    var town = TOWNS[pick];
    var cost = fare(town[2]);
    if (!cm.askYesNo("ここではもう用事がないようですね。本当に#b" + town[0] + "#kへ移動しますか？(" + cost + "メル)")) return;
    if (player.getMeso() < cost) {
        cm.sendOk("メルが足りないようだな。料金は" + cost + "メルだ。");
        return;
    }
    player.gainMeso(-cost);
    player.warp(town[1]);
}
