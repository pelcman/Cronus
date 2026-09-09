// ビクトリアアイランドの封印石 (quest 21735, 受注 NPC 1002104 トゥルー → 完了 NPC 1201000 リリン, Lv37) — 封印石(4032323)をリリンに預ける。
// 封印石の付与は JMS の Act[0] がデータ経路で行う。EXP 6037 は Cosmic の値(JMS の Act[1] は空)。出典 Reference/Cosmic/scripts/quest/21735.js。
// JMS: 開始 q21735s / 終了 q21735e、完了は 4032323×1。台詞は創作。
function start() {
    qm.sendNext("アラン、人形使いに襲われて以来、封印石をここに置いておくのが不安でな。リリンに預けてきてくれないか。");
    player.startQuest(21735);
}

function end() {
    if (!player.haveItem(4032323)) {
        qm.sendOk("封印石は…持っていないのですか？　トゥルーからもう一度受け取ってきてください。");
        return;
    }
    qm.sendNext("#r#p1002104##k が、安全のために #b#t4032323##k をここへ？　…分かりました。私が責任を持って預かります。");
    player.gainItem(4032323, -1);
    player.gainExp(6037);
    player.completeQuest(21735);
}
