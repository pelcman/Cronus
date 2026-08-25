// カイジ (ヘネシスゲームパーク, wz script = minigame00) — ミニゲームの案内と道具の販売。
// 販売価格はオラクルが無いため簡易設定(創作)。セット類は実データ検証済みID。
var GOODS = [
    [4080000, "五目並べセット",        5000],
    [4080001, "五目並べセット(石ころ)",  5000],
    [4080002, "五目並べセット(どんぐり)", 5000],
    [4080003, "五目並べセット(ソンピョン)", 5000],
    [4080004, "五目並べセット(貝)",     5000],
    [4080100, "神経衰弱セット",        3000]
];
function start() {
    var pick = cm.askMenu("ここはゲームパーク!オモック(五目並べ)や神経衰弱で遊べるよ。"
        + "\r\n#L0#遊び方を聞く#l"
        + "\r\n#L1#ゲームセットを買う#l");
    if (pick == 0) {
        cm.sendNext("オモックや神経衰弱で遊ぶには#bゲームセット#kが必要だよ。"
            + "\r\nセットを持った人がその場で部屋を開いて、相手が入ってきたら対戦開始!");
        cm.sendOk("部屋を開くにはセットをダブルクリック。#b自由市場や町の中#kならどこでも開けるよ。"
            + "\r\n負けても経験値は減らないから安心して遊んでね。");
        return;
    }
    if (pick != 1) return;
    var menu = "どのセットにする?";
    for (var i = 0; i < GOODS.length; i++) {
        menu += "\r\n#L" + i + "#" + GOODS[i][1] + " (" + GOODS[i][2] + "メル)#l";
    }
    var g = cm.askMenu(menu);
    if (g < 0 || g >= GOODS.length) return;
    var item = GOODS[g];
    if (player.getMeso() < item[2]) {
        cm.sendOk("メルが足りないよ。" + item[2] + "メル持ってきてね。");
        return;
    }
    player.gainMeso(-item[2]);
    player.gainItem(item[0], 1);
    cm.sendOk("まいど!#b#t" + item[0] + "##k #i" + item[0] + "#\r\n楽しんでね!");
}
