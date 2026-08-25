// 遠征隊の標識 (ホーンテイルの洞窟入口, wz script = hontale_accept) — ボス洞窟への入場。
// 本来の遠征隊(人数・時間管理)は未実装のため、警告付きの入場として簡易実装。
function start() {
    if (!cm.askYesNo("#rこの先はホーンテイルの領域だ。#k生半可な力では生きて帰れない。"
        + "\r\n#b試験の洞窟#kへ進むか?")) {
        return;
    }
    player.warp(240060000);
}
