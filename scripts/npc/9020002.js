// ネーラ — 「一つ目の同行」からの退場
function start() {
    if (cm.askYesNo("パーティークエストをやめて、カニングシティへ戻りますか?")) {
        player.warp(103000000);
    }
}
