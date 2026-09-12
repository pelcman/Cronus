// ゴミ箱 1092018 (ノーチラス 上階廊下 120000100, JMS スクリプト名 nautil_letter) — 書きかけの捨てられた手紙(2162)を終える前に調べると クシャクシャの便箋(4031839) が
// 見つかる(一度に 1 枚)。出典 Reference/Cosmic/scripts/npc/1092018.js。台詞は創作。
function start() {
    if (!player.isQuestDone(2162) && !player.haveItem(4031839)) {
        player.gainItem(4031839, 1);
        cm.sendOk("（ゴミ箱からはみ出していたクシャクシャの便箋を拾い上げた。何か大事なことが書いてあるようだ。）");
        return;
    }
    cm.sendOk("（ゴミ箱だ。気になるものはもう無い。）");
}
