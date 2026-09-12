// 王宮オアシス 2103000 (アリアント宮殿 260000300, JMS スクリプト名 ariant_oasis) — アリアントの文化習い(3900)の進行中に水を飲むと記録 3900 を "5" にする(Cosmic の
// setQuestProgress)。Cosmic にあるティガン変身薬(2210005)を拾う分岐は、変身バフの判定 API が無いため未実装。出典 Reference/Cosmic/scripts/npc/2103000.js。台詞は創作。
function start() {
    if (player.hasQuest(3900) && player.getQuestData(3900) != "5") {
        cm.sendOk("#b（オアシスの水を飲んだ。生き返るようだ。）#k");
        player.setQuestData(3900, "5");
        return;
    }
    cm.sendOk("（宮殿の庭に、澄んだ泉が湧いている。）");
}
