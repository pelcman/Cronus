// 妖精アルウェン 1032100 (エリニア 101000000, JMS スクリプト名 owen) — 妖精の錬金術師。Lv40 以上に 月の石(4011007) / 星の石(4021009) / 黒い羽(4031042) を作る。
// 月の石: 青銅〜金の精錬板 7 種 + 10,000 メソ、星の石: 宝石 9 種 + 15,000 メソ、黒い羽: 炎の羽毛 + 月の石 + 黒水晶 + 30,000 メソ。
// 出典 Reference/Cosmic/scripts/npc/1032100.js。台詞は創作。
function haveAll(from, to) {
    for (var i = from; i <= to; i++) {
        if (!player.haveItem(i)) {
            return false;
        }
    }
    return true;
}

function takeAll(from, to) {
    for (var i = from; i <= to; i++) {
        player.gainItem(i, -1);
    }
}

function start() {
    if (player.getLevel() < 40) {
        cm.sendOk("珍しくて価値のあるものを作れるけれど、残念ながら見知らぬ人には作ってあげられないの。");
        return;
    }
    cm.sendNext("ええ…私は妖精の錬金術師。本当は妖精が人間と長く関わってはいけないのだけど…あなたほど強い人なら大丈夫ね。材料を持ってきてくれれば、特別なものを作ってあげる。");
    var pick = cm.askMenu("何を作る？#b\r\n#L0#月の石#l\r\n#L1#星の石#l\r\n#L2#黒い羽#l");
    if (pick == 0) {
        if (!cm.askYesNo("月の石を作りたいのね？　それには #b青銅#k、#b鋼鉄#k、#bミスリル#k、#bアダマンティウム#k、#b銀#k、#bオリハルコン#k、#b金#k の精錬板が 1 枚ずつ必要よ。それと 10,000 メソ。作る？")) {
            cm.sendOk("月の石を作るのは簡単じゃないの。材料を準備してきてね。");
            return;
        }
        if (!haveAll(4011000, 4011006) || player.getMeso() < 10000) {
            cm.sendOk("メソは足りている？　#b青銅#k、#b鋼鉄#k、#bミスリル#k、#bアダマンティウム#k、#b銀#k、#bオリハルコン#k、#b金#k の精錬板が 1 枚ずつあるか確かめてね。");
            return;
        }
        player.gainMeso(-10000);
        takeAll(4011000, 4011006);
        player.gainItem(4011007, 1);
        cm.sendOk("はい、月の石よ。いい材料を使ったから、よくできているわ。また私の助けが必要になったら、いつでも来てね。");
        return;
    }
    if (pick == 1) {
        if (!cm.askYesNo("星の石を作りたいのね？　それには #bガーネット#k、#b紫水晶#k、#bアクアマリン#k、#bエメラルド#k、#bオパール#k、#bサファイア#k、#bトパーズ#k、#bダイヤモンド#k、#b黒水晶#k が 1 個ずつ必要よ。それと 15,000 メソ。作る？")) {
            cm.sendOk("星の石を作るのは簡単じゃないの。材料を準備してきてね。");
            return;
        }
        if (!haveAll(4021000, 4021008) || player.getMeso() < 15000) {
            cm.sendOk("メソは足りている？　#bガーネット#k、#b紫水晶#k、#bアクアマリン#k、#bエメラルド#k、#bオパール#k、#bサファイア#k、#bトパーズ#k、#bダイヤモンド#k、#b黒水晶#k が 1 個ずつあるか確かめてね。");
            return;
        }
        player.gainMeso(-15000);
        takeAll(4021000, 4021008);
        player.gainItem(4021009, 1);
        cm.sendOk("はい、星の石よ。いい材料を使ったから、よくできているわ。また私の助けが必要になったら、いつでも来てね。");
        return;
    }
    if (pick == 2) {
        if (!cm.askYesNo("黒い羽を作りたいのね？　それには #b炎の羽毛 1 枚#k、#b月の石 1 個#k、#b黒水晶 1 個#k が必要よ。それと 30,000 メソ。作る？　この羽はとても特別なものだから、落とすと消えてしまうし、人に渡すこともできないわ。")) {
            cm.sendOk("黒い羽を作るのは簡単じゃないの。材料を準備してきてね。");
            return;
        }
        if (!(player.haveItem(4001006) && player.haveItem(4011007) && player.haveItem(4021008)) || player.getMeso() < 30000) {
            cm.sendOk("メソは足りている？　#b炎の羽毛#k、#b月の石#k、#b黒水晶#k が 1 つずつあるか確かめてね。");
            return;
        }
        player.gainMeso(-30000);
        player.gainItem(4001006, -1);
        player.gainItem(4011007, -1);
        player.gainItem(4021008, -1);
        player.gainItem(4031042, 1);
        cm.sendOk("はい、黒い羽よ。いい材料を使ったから、よくできているわ。また私の助けが必要になったら、いつでも来てね。");
    }
}
