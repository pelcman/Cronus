// 暗証番号の認証 (quest 3360, NPC 2111006 ファウェン) — 秘密通路のマスターキー(10 桁の英数字)を教わり、通路の扉(261010000 / 261020200 の
// ポータル secret00 → 秘密通路 NPC 2111024)で入力すると認証される。キーはクエスト記録 3360 に保存し、扉が認証したら "1" に置き換える
// (JMS の完了条件 infoex value=1)。出典 Reference/Cosmic/scripts/quest/3360.js。JMS: 開始スクリプト q3360s、Check は 3359 完了と Lv70。台詞は創作。
function start() {
    qm.sendNext("おお！　やっと来たか！　間に合ってよかった。お前に渡すマスターキーができたぞ。");
    if (!qm.askAccept("いいか、このキーはとても長くて複雑だ。しっかり覚えてもらう必要がある。準備はいいか？　紙とペンを用意しておけよ。")) {
        qm.sendOk("さあ早く、準備ができたらまた来い。覚えられる自信がないなら、紙とペンを出しておけ！");
        return;
    }
    var chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    var key = "";
    for (var i = 0; i < 10; i++) {
        key += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    qm.sendOk("キーコードは #b" + key + "#k だ。覚えたか？　このキーを秘密通路の扉に入力すれば、通り抜けられるようになる。");
    player.startQuest(3360);
    player.setQuestData(3360, key);
}
