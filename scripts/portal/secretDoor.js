// 秘密通路の扉 (研究所1階廊下 261010000 / 研究所B-1区域 261020200 のポータル secret00): 暗証番号の認証(3360)を済ませていれば
// 暗黒の魔法使いの研究室へ続く道 261030000 へ(戻り側は sp_jenu / sp_alca)。まだなら秘密通路 NPC 2111024 が認証を求める(未受注なら閉ざされたまま)。
// 出典 Reference/Cosmic/scripts/portal/secretDoor.js。
function start() {
    if (player.isQuestDone(3360) || player.getQuestData(3360) == "1") {
        player.warpPortal(261030000, player.getMapId() == 261010000 ? "sp_jenu" : "sp_alca");
        return;
    }
    player.openNpc(2111024);
}
