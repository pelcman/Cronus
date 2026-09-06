// 出口 (3号線工事場 B1〜B3, wz script = subway_out) — 工事場から切符売り場(103000100)へ戻る装置。
// 台詞は OdinMS 系の英文を日本語化(創作)。
function start() {
    if (!cm.askYesNo("この装置は外につながっている。ここを諦めて出るのか？次に入る時はまた最初からになるが…")) return;
    player.warp(103000100);
}
