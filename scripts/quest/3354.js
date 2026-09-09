// ドランの薬 (quest 3354, 受注 NPC 2111002 ドラン → 完了 2111001 マッド) — ドランが設計した薬をマッドに頼みに行く。
// 完了側はデータ経路(EXP 100)。出典 Reference/Cosmic/scripts/quest/3354.js。JMS: 開始スクリプト q3354s、Check は 3353 完了。台詞は創作。
function start() {
    if (!qm.askAccept("頼みがある。私が設計した薬を、#bマッド#k に作ってもらってきてくれないか？　もちろん、私のことは決して口にしないでくれ。")) {
        qm.sendOk("…そうか。無理にとは言わない。");
        return;
    }
    player.startQuest(3354);
}
