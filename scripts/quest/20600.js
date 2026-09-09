// 終わらない修行 (quest 20600, NPC 1101002 ナインハート) — Lv100 の騎士に修行を続けるよう命じる。承諾で受注(完了側はデータ経路)。
// 出典 Reference/Cosmic/scripts/quest/20600.js。JMS: 開始スクリプト q20600s、normalAutoStart、Lv100、Act は空。台詞は創作。
function start() {
    if (!qm.askAccept("#h0#。レベル 100 に届いてから、修行を怠ってはいないだろうな？　騎士の修行に終わりはない。次の課題に挑む気はあるか？")) {
        return;
    }
    player.startQuest(20600);
}
