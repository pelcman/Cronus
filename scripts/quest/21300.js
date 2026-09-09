// 主妨げし武器 (quest 21300, 受注 NPC 1201000 リリン → 完了 NPC 1201002 マッハ, Lv70) — マッハの様子がおかしいと聞き、リエンへ戻る。承諾で受注(完了側はデータ経路)。
// 出典 Reference/Cosmic/scripts/quest/21300.js。JMS: 開始スクリプト q21300s、normalAutoStart、Act は空。台詞は創作。
function start() {
    qm.sendNext("修行はどうですか？　ふむ…レベル 70。まだまだ、ですね。");
    if (!qm.askAccept("ですがその前に、しばらくリエンに戻ってほしいのです。#p1201002# の様子が、どうもおかしくて。来てくれますか？")) {
        qm.sendOk("（少し考える時間が必要だ…。）");
        return;
    }
    player.startQuest(21300);
    qm.sendOk("武器がひとりでに震えるなんて、ただごとではありません。何かを訴えているのかも。急いでください。");
}
