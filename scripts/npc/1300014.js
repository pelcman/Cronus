// セルフ 1300014 (JMS スクリプト名 forself): キノコ城の調査ポイントで開く「独り言」NPC。JMS のスクリプト本体は参照に無く、Quest Check の
// infoNumber/infoex(クエスト情報 "1" で完了できるようになる)から推定して、進行中の探査クエストの印を立てる。蔓の壁での足止めと
// 蔓刺除去剤の使用は推定なので [DEV]。台詞は創作。
function start() {
    var map = player.getMapId();
    if (map == 106020300) {
        if (player.hasQuest(2314) && player.getQuestData(2314) != "1") {
            cm.sendNext("（トゲの蔓が壁のように道をふさいでいる…普通の蔓じゃない。強い魔力を感じる。）");
            player.setQuestData(2314, "1");
            cm.sendOk("（よし、これを #b#p1300003##k に報告しよう。）");
            return;
        }
        if (!player.hasQuest(2321) && !player.isQuestDone(2321)) {
            cm.sendDev("（トゲの蔓に阻まれて、この先へは進めない…）");
            return;
        }
        cm.sendOk("（トゲの蔓の壁だ。殺キノコスプレーがあれば通れるはず。）");
        return;
    }
    if (map == 106020500) {
        if (player.hasQuest(2322) && player.getQuestData(2322) != "1") {
            cm.sendNext("（城壁の入口が完全にふさがれている…蔓の刺がびっしりだ。これでは登れない。）");
            player.setQuestData(2322, "1");
            cm.sendOk("（#b#p1300003##k に報告しよう。）");
            return;
        }
        if (player.hasQuest(2324) && player.getQuestData(2324) != "1") {
            if (!player.haveItem(2430015)) {
                cm.sendOk("（#b#t2430015##k がないと、この刺は取り除けない。）");
                return;
            }
            player.gainItem(2430015, -1);
            player.setQuestData(2324, "1");
            cm.sendDev("（#b#t2430015##k を撒いた…刺がしおれて落ちていく。これで城壁を登れる。分かれ道から城へ向かおう。）");
            return;
        }
        cm.sendOk("（城壁の端だ。刺がなければ、分かれ道から城壁へ登れるはず。）");
        return;
    }
    cm.sendDev("（…特に何もない。）");
}
