// 邪気の森1 100040105 → 呪われた森 910100000 (ファウスト退治 2227 完了後は 910100001) の out00。恨み、恨み、寃魂(2224) か アルウェンの謝り(2226)
// の進行中、または 2227 完了後にだけ入れ、時刻は 0〜6 時か 17 時以降(サーバー時刻)。出典 Reference/Cosmic/scripts/portal/curseforest.js。
function start() {
    if (!(player.hasQuest(2224) || player.hasQuest(2226) || player.isQuestDone(2227))) {
        player.message("ここには入れない。");
        return;
    }
    var h = player.hourOfDay();
    if (!(h < 7 || h >= 17)) {
        player.message("今はこの場所に入れない。夜になってから来よう。");
        return;
    }
    player.warpPortal(player.isQuestDone(2227) ? 910100001 : 910100000, "out00");
}
