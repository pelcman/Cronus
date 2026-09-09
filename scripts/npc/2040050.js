// 風来坊錬金術師 (Wandering Alchemist, NPC 2040050) — 素材と鉱石とメソから「魔法の石」「召喚の石」を各5個作る。
// 出典 Reference/Cosmic/scripts/npc/2040050.js を JMS v186 に移植。台詞は JMS 原文が手元に無いため創作、
// レシピの素材/鉱石/生成物 ID・数量・費用(4000メソ)は Cosmic のまま。全 22 種の ID が JMS v186 の
// String.wz に実在することを DevTools で確認済み(魔法の石 4006000 / 召喚の石 4006001、水晶 4021xxx、鉱石 4011xxx)。
// 注: Cosmic の canHold(空きスロット確認)は Cronus の INpcPlayer に無いため省略(etc 欄が満杯のときだけ影響、低リスク)。

var MADE = [4006000, 4006001]; // 魔法の石 / 召喚の石
var COST = 4000;
var RECIPES = [
    // 魔法の石: [素材A[id,数], 素材B[id,数], 水晶[id,1]]
    [[[4000046, 20], [4000027, 20], [4021001, 1]],
     [[4000025, 20], [4000049, 20], [4021006, 1]],
     [[4000129, 15], [4000130, 15], [4021002, 1]],
     [[4000074, 15], [4000057, 15], [4021005, 1]],
     [[4000054, 7],  [4000053, 7],  [4021003, 1]]],
    // 召喚の石: [素材A, 素材B, 鉱石[id,1]]
    [[[4000046, 20], [4000027, 20], [4011001, 1]],
     [[4000014, 20], [4000049, 20], [4011003, 1]],
     [[4000132, 15], [4000128, 15], [4011005, 1]],
     [[4000074, 15], [4000069, 15], [4011002, 1]],
     [[4000080, 7],  [4000079, 7],  [4011004, 1]]]];

function start() {
    cm.sendNext("カエルの舌とリスの歯を混ぜて…おっと、キラキラ光る白い粉を入れ忘れるところだった！　ふぅ…おや、いつからそこに？　仕事に夢中でな、はは。");

    var kind = cm.askMenu("見てのとおり、わしは旅の錬金術師じゃ。修行中の身だが、お前さんに要りそうな物なら作れるぞ。見ていくかね？\r\n\r\n"
        + "#b#L0#魔法の石を作る#l\r\n#L1#召喚の石を作る#l#k");
    if (kind != 0 && kind != 1) {
        cm.dispose();
        return;
    }

    var made = MADE[kind];
    var recipes = RECIPES[kind];
    var menu = "#b#t" + made + "##k は、わしにしか作れぬ神秘の石じゃ。作り方は5通りある。どれで作るかね？";
    for (var i = 0; i < recipes.length; i++) {
        menu += "\r\n#L" + i + "##t" + recipes[i][0][0] + "# と #t" + recipes[i][1][0] + "# で作る#l";
    }
    var r = cm.askMenu(menu);
    if (r < 0 || r >= recipes.length) {
        cm.dispose();
        return;
    }

    var req = recipes[r];
    var need = "#b#t" + made + "##k を5個作るには、以下が要る。狩りで集められる物ばかりじゃ。作るかね？\r\n";
    for (var j = 0; j < req.length; j++) {
        need += "\r\n#v" + req[j][0] + "# #b" + req[j][1] + " #t" + req[j][0] + "#s#k";
    }
    need += "\r\n#i4031138# #b" + COST + " メソ#k";
    if (!cm.askYesNo(need)) {
        cm.sendNext("素材が足りぬか？　なに、集まってからまた来ればよい。狩りでも取引でも、手に入れる方法はいくらでもあるでな。");
        return;
    }

    var ok = player.getMeso() >= COST;
    for (var k = 0; k < req.length; k++) {
        if (player.itemQuantity(req[k][0]) < req[k][1]) {
            ok = false;
        }
    }
    if (!ok) {
        cm.sendOk("必要な素材が揃っているか、メソが足りているか、確かめてから来ておくれ。");
        return;
    }

    for (var m = 0; m < req.length; m++) {
        player.gainItem(req[m][0], -req[m][1]);
    }
    player.gainMeso(-COST);
    player.gainItem(made, 5);
    cm.sendOk("さあ、#b#t" + made + "##k を5個持っていくがよい。我ながら会心の出来じゃ。また入り用なら訪ねてこい！");
}
