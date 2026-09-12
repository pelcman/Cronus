// 忘れられた神殿 910500200 の仕掛け 1050000: 3 割の確率でアイテム(リアクターのドロップ表、サーバーが処理)、7 割で 別世界への扉 105090200 へ飛ばされる。
// 出典 Reference/Cosmic/scripts/reactor/1050000.js。
function start() {
    if (Math.random() > 0.7) {
        return;
    }
    player.warp(105090200, 0);
}
