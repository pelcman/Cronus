// 4番目の情報収集 完了 (quest 21729, 受注 NPC 1061019 ニッシ → 完了 NPC 1002104 トゥルー, Lv28) — 承諾で受注(完了側はデータ経路、EXP 500)。
// 出典 Reference/Cosmic/scripts/quest/21729.js。JMS: 開始スクリプト q21729s、Check は 21728 完了。台詞は創作。
function start() {
    qm.sendNext("よし、これ以上はトゥルーに戻って詳しい話を聞くべきじゃない。まずは #p1002104# に、ここで分かったことを伝えてくれ。");
    player.startQuest(21729);
}
