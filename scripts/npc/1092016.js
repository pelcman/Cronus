// 輝く石 1092016 (ノーチラス 動力室 120000301, JMS スクリプト名 nautil_stone) — 光る石(2166)の進行中に触れると完了する。出典 Reference/Cosmic/scripts/npc/1092016.js。台詞は創作。
function start() {
    if (player.hasQuest(2166)) {
        cm.sendNext("（美しく輝く石だ。周りに不思議な力を感じる。）");
        player.completeQuest(2166);
        return;
    }
    cm.sendOk("（輝く石に手を触れると、不思議な力が体の中に流れ込んでくるのを感じた。）");
}
