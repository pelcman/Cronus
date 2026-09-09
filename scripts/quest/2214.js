// 沼地小屋 (quest 2214, 完了 NPC 1052108 倒れたコミ箱) — 夕方(17〜20時)にゴミ箱を探すと、もみくちゃの紙切れが見つかる。
// 出典 Reference/Cosmic/scripts/quest/2214.js を JMS v186 に移植。JMS: 終了スクリプト q2214e、Act1 で 4031894 を付与(完了処理で
// 渡されるのでスクリプトでは渡さない)。Cosmic の EXP 20000 は JMS の Act に無いため付けない(数値の根拠が無い)。台詞は創作。
function end() {
    var h = player.hourOfDay();
    if (h < 17 || h >= 20) {
        qm.sendOk("（うーん、ゴミ箱を探してみたけど、ゼイエムが言っていた #t4031894# は見当たらない…まだ時間じゃないのかも。）");
        return;
    }
    qm.sendNext("（あ、くしゃくしゃになったメモがある…何かの企みについて書いてある。裏通りのゼイエムが言っていたのはこれか。）");
    player.completeQuest(2214);
}
