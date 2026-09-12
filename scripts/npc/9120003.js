// ヒカリ 9120003 (ショーワ町 801000000, JMS スクリプト名 in_bath) — 300 メソで銭湯へ。男は 脱衣所（男） 801000100、女は 脱衣所（女） 801000200 の out00。
// 出典 Reference/Cosmic/scripts/npc/9120003.js。台詞は創作。
function start() {
    if (!cm.askYesNo("銭湯に入っていく？　300 メソだよ。")) {
        cm.sendOk("またいつでも来てね。");
        return;
    }
    if (player.getMeso() < 300) {
        cm.sendOk("300 メソ持っているか、確かめてね。");
        return;
    }
    player.gainMeso(-300);
    player.warpPortal(player.getGender() == 0 ? 801000100 : 801000200, "out00");
}
