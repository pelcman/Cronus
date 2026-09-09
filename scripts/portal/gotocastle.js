// 分かれ道 106020400 から城壁へ向かう道(ポータル gotocastle)。JMS のスクリプト本体は参照に無いので Quest Check から推定:
// 城壁を越えて4 (2324) で蔓刺除去剤を使った後(クエスト情報 "1")か 2324 完了なら、刺の消えた 城壁の端 106020501(top00 で 外郭城壁 106020600 へ)、
// それ以外は行き止まりの 城壁の端 106020500(調査ポイント investigate2)。
function start() {
    if (player.isQuestDone(2324) || player.getQuestData(2324) == "1") {
        player.warp(106020501, 0);
        return;
    }
    player.warp(106020500, 0);
}
