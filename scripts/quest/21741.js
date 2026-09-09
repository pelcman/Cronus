// 武陵の調査  (quest 21741, 受注 NPC 1002104 トゥルー → 完了 NPC 2090004 チエル, Lv54) — 武陵のチエルがブラックウイングと接触したという話。承諾で受注(完了側はデータ経路、EXP 500)。
// 出典 Reference/Cosmic/scripts/quest/21741.js。JMS: 開始スクリプト q21741s、normalAutoStart。台詞は創作。
function start() {
    qm.sendNext("レベルは上がっているか？　面白い話を仕入れたんだ。");
    if (!qm.askAccept("武陵の #bチエル#k が、どうやらブラックウイングの者と会ったらしい。話を聞いてきてくれないか？")) {
        return;
    }
    qm.sendNext("チエルは無愛想で知られている。粘り強く話を聞くんだぞ。");
    player.startQuest(21741);
}
