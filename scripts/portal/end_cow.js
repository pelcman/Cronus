// ノーチラスの牛小屋 912000100 → 食堂 120000103: 新鮮な牛乳を手に入れる(2180)の進行中にミルクボトル(4031847〜4031850)を持っていれば、
// 満タンのボトル(4031850)のときだけ出られる。クエスト外なら普通に出る。出典 Reference/Cosmic/scripts/portal/end_cow.js。
function start() {
    if (player.hasQuest(2180) && (player.haveItem(4031847) || player.haveItem(4031848) || player.haveItem(4031849) || player.haveItem(4031850))) {
        if (!player.haveItem(4031850)) {
            player.message("ミルクボトルはまだ満タンではない…。");
            return;
        }
    }
    player.warp(120000103, 0);
}
