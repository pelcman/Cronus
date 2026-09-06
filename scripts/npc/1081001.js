// パイソン (フロリナビーチ, wz script = florina1) — 乗船元(リス港/オルビス/ルディブリアム)へ戻る船。
// 戻り先は送迎NPCが記憶したマップ、記憶が無ければリス港(104000000)。台詞は簡易(創作、Riremitoさん版も「テキスト適当」)。
function start() {
    if (!cm.askYesNo("ビーチはもう十分楽しんだか？乗ってきた港へ戻るなら船を出すぜ。戻るか？")) return;
    player.warpToRememberedMap(104000000);
}
