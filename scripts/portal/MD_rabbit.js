// ミニダンジョン ビートラビットの隠れ家 (入口: エオス塔76階-90階 221023400 のポータル MD00 / 中の出口: ビートラビットの隠れ家 221023401 の out00)。
// JMS v186 では 221023401 だけが本物(モブ・ポータル入り)で、続く 40 個のコピー枠は中身が空(サーバーが複製して使う想定)。Cronus は
// まだフィールドの複製(インスタンス)を持たないので実マップ 1 部屋を使う: 誰もいないか自分のパーティーが中にいるときだけ入れる [DEV]。
// 出典 Reference/Cosmic/scripts/portal/MD_rabbit.js(パーティーはリーダーだけが開けて全員をワープ; ここでは中にいる仲間を追って入れる形に簡略化)。
var base = 221023400;
var dungeon = 221023401;

function start() {
    if (player.getMapId() == base) {
        if (!player.enterMiniDungeon(dungeon)) {
            player.message("[DEV] ミニダンジョンは今ほかの人が使っています。しばらくしてからもう一度お試しください。");
        }
        return;
    }
    player.warpPortal(base, "MD00");
}
