// 帰還碑 (Return Monument, NPC 9040005) — シャレニアン ギルドクエストの各マップから出る。
// 出典 Reference/Cosmic/scripts/npc/9040005.js を JMS v186 に移植。行き先 101030104(遺跡発掘ベースキャンプ)は JMS に実在。
// 台詞は JMS 原文が手元に無いため創作。ギルドクエスト本体はまだ未実装(退出だけ動く)。
function start() {
    if (cm.askYesNo("ギルドクエストから退出しますか？")) {
        player.warp(101030104, 0);
    }
}
