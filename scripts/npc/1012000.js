// メイプル運輸大型タクシー (ヘネシス, wz script = taxi2) — ビクトリアアイランド6町へのタクシー(乗り場の町を除く)。
// 台詞は JMS 原文(Riremito/jms_scripts より)。料金はオラクルに値が無いため簡易(創作):
// 1000メル(リス港のみ800)、初心者(職業0)は1/10。行き先はこの6町に限定(選択肢IDはエンジン側で検証される)。
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
    cm.sendNext("こんにちは！　メイプル運輸大型タクシーでございます。他の村への安全で迅速な移動をお望みですか？でしたら我がタクシーをご利用ください。安い値段でお望みの場所まで親切にご案内しております。");
    var here = player.getMapId();
    var menu = "目的地をお選びください。村事に料金が異なります。";
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
        cm.sendOk("メルが足りないようですね。料金は" + cost + "メルです。");
        return;
    }
    player.gainMeso(-cost);
    player.warp(town[1]);
}
