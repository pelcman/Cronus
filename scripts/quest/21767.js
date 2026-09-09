// 木箱の秘密 (quest 21767, 受注 NPC 1204033 ゾーンの箱 → 完了 NPC 20000 ゾーン, Lv13) — 箱の中の関節炎治療剤(4032423)を見つけて、ゾーンに返す。
// 完了側はデータ経路(4032423 回収、EXP 250、デンデンのカラ ×10 ×3 種)。出典 Reference/Cosmic/scripts/quest/21767.js。JMS: 開始スクリプト q21767s、Check は 21766 進行中。台詞は創作。
function start() {
    qm.sendNext("#b（ふむ、箱の中に薬のようなものが入っている。いったい何の薬だろう…？　#p20000# に聞いてみよう。）#k");
    if (!player.haveItem(4032423)) {
        player.gainItem(4032423, 1);
    }
    player.startQuest(21767);
}
