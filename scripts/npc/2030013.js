// ザクムへの扉 — 祭壇への入場と「火の目」の頒布 (簡略版: プレクエの代わりに有償頒布)
var EYE = 4001017;   // 火の目
var EYE_COST = 50000;
function start() {
    var pick = cm.askMenu("この先はザクムの祭壇……火と岩の魔神が眠る場所だ。"
        + "\r\n#L0#祭壇へ入場する#l"
        + "\r\n#L1#「火の目」を受け取る (" + EYE_COST + "メル)#l"
        + "\r\n#L2#ザクムについて聞く#l");
    if (pick == 0) {
        player.warp(280030000);
        return;
    }
    if (pick == 1) {
        if (player.haveItem(EYE)) {
            cm.sendOk("すでに「火の目」を持っているではないか。祭壇に捧げてくるがいい。");
            return;
        }
        if (player.getMeso() < EYE_COST) {
            cm.sendOk("供物の準備金が足りない。" + EYE_COST + "メル必要だ。");
            return;
        }
        player.gainMeso(-EYE_COST);
        player.gainItem(EYE, 1);
        cm.sendOk("これが「火の目」だ。祭壇に捧げればザクムが目覚める。"
            + "本来は厳しい試練を越えた者だけが手にできるのだがな……。");
        return;
    }
    cm.sendOk("ザクムは腕を全て砕くまで本体に傷ひとつ付かん。"
        + "本体は倒しても二度姿を変える。長い戦いになるぞ、回復薬を忘れるな。");
}
