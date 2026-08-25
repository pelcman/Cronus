// ルイス (忍耐の森, wz script = herb_out) — 修練を諦めてエリニアへ戻る出口係。
function start() {
    if (cm.askYesNo("忍耐の森はここで終わりじゃないよ。それでも外へ戻るかい?\r\n#b(エリニアへ戻ります)#k")) {
        player.warp(101000000);
    } else {
        cm.sendOk("その意気だ!上を目指してがんばって。");
    }
}
