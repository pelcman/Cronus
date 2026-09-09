// dojang_next — 武陵道場: 次の階へ。現在の階の修練点数を加算し、次のステージ(現マップ+100の同コピー)へ
// 進む。38階なら報酬を受け取って屋上(925020003)へ。ボスと制限時間はマップ入場時に付与される。
// 出典: Reference/Cosmic/scripts/portal/dojang_next.js + Event_DojoAgent.warpNextMap。
function start() {
    player.dojoNextStage();
}
