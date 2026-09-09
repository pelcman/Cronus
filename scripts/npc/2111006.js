// ファウェン 2111006 (JMS スクリプト名 drang_room1, 関係者以外出入禁止区域 261020401) — ドラン関連のクエストは quest/33xx.js 側。NPC を直接
// クリックしたときは、ファウェンが知っていること(3320)を受けた後ならドランの研究室 926120200 へ送る。出典 Reference/Cosmic/scripts/npc/2111006.js
// (Cosmic は即ワープ、ここでは確認を挟む)。台詞は創作。
function start() {
    if (player.hasQuest(3320) || player.isQuestDone(3320)) {
        if (cm.askYesNo("ドラン博士の研究室へ行くか？")) {
            player.warp(926120200, 0);
        }
        return;
    }
    cm.sendOk("うう…どうしてここは幽霊ばかりなんだ…。");
}
