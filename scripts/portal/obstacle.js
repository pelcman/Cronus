// 奥深きキノコの森 106020300 のトゲの蔓の壁(ポータル obstacle)。JMS のスクリプト本体は参照に無いので Quest Check から推定:
// 城壁を越えて1 (2321、殺キノコスプレー完成後に魔法大臣が「これで蔓の壁を抜けられる」と出す) を受けた後なら 分かれ道 106020400 へ通し、
// 手前ならセルフ NPC の独り言([DEV])で断る。
function start() {
    if (player.hasQuest(2321) || player.isQuestDone(2321)) {
        player.warp(106020400, 0);
        return;
    }
    player.openNpc(1300014);
}
