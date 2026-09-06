// ウンイ (地下鉄 切符売り場, wz script = subway_ticket) — 3号線工事場B1〜B3の入場券販売。
// レベル帯(20+/30+/40+)と価格(500/1200/2000)は OdinMS 系スクリプト準拠、台詞は日本語化(創作)。入場券IDは実データ検証済み。
var TICKETS = [
    ["工事場B1", 4031036, 500, 20],
    ["工事場B2", 4031037, 1200, 30],
    ["工事場B3", 4031038, 2000, 40]
];
function start() {
    var lv = player.getLevel();
    if (lv < 20) {
        cm.sendOk("切符を買えば工事場に入れますが、あなたにはまだ危険すぎるようです。地下には妙な仕掛けがたくさんありますから、もっと鍛えてから来てください。");
        return;
    }
    var menu = "切符を買えば右の#b改札口#kから工事場に入れます。中は仕掛けが多いですが、珍しい品が眠っているとか。どの切符を買いますか？";
    for (var i = 0; i < TICKETS.length; i++) {
        if (lv >= TICKETS[i][3]) menu += "\r\n#L" + i + "##b" + TICKETS[i][0] + "入場券 (" + TICKETS[i][2] + "メル)#k#l";
    }
    var pick = cm.askMenu(menu);
    if (pick < 0 || pick >= TICKETS.length || lv < TICKETS[pick][3]) return;
    var t = TICKETS[pick];
    if (!cm.askYesNo("#b#t" + t[1] + "##kを" + t[2] + "メルで購入しますか？その他(ETC)欄に空きがあるか確認してください。")) return;
    if (player.getMeso() < t[2]) {
        cm.sendOk("メルが足りないようです。");
        return;
    }
    player.gainMeso(-t[2]);
    player.gainItem(t[1], 1);
    cm.sendOk("切符は#b改札口#kに入れてください。仕掛けが多いので気をつけて。");
}
