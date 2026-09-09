// オルビス調査  (quest 21736, 受注 NPC 1002104 トゥルー → 完了 NPC 2012012 リーサ, Lv45) — オルビスの異変を妖精リーサに聞きに行く。承諾で受注(完了側はデータ経路、EXP 500)。
// 出典 Reference/Cosmic/scripts/quest/21736.js。JMS: 開始スクリプト q21736s、normalAutoStart。台詞は創作。
function start() {
    qm.sendNext("久しぶりだな！　前に会ったときより、ずいぶんレベルが上がったじゃないか。");
    qm.sendNext("さて、世間話はここまでだ。ブラックウイングの動きを、もっと広く調べるべきだと気づいてな。");
    qm.sendNext("どうやら #bオルビス#k で、おかしなことが起きているらしい。");
    if (!qm.askAccept("オルビスの #b妖精リーサ#k なら、何か知っているはずだ。会いに行ってくれるか？")) {
        return;
    }
    player.startQuest(21736);
}
