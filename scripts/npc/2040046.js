// 友達リスト管理 — 友達リストの枠を5つ拡張 (最大100)
var COST = 230000;
var STEP = 5;
var MAX = 100;
function start() {
    var cap = player.getBuddyCapacity();
    if (cap >= MAX) {
        cm.sendOk("あなたの友達リストはもう最大の" + MAX + "人分ですよ。たいした人気者ですね。");
        return;
    }
    if (!cm.askYesNo("友達リストの枠を" + STEP + "つ増やせますよ。料金は" + COST + "メル。"
            + "今の枠は" + cap + "人分です。拡張しますか?")) {
        cm.sendOk("また必要になったら来てくださいね。");
        return;
    }
    if (player.getMeso() < COST) {
        cm.sendOk("メルが足りないようですね。料金は" + COST + "メルです。");
        return;
    }
    player.gainMeso(-COST);
    player.gainBuddyCapacity(STEP);
    cm.sendOk("はい、拡張しました!これで" + player.getBuddyCapacity() + "人まで登録できますよ。");
}
