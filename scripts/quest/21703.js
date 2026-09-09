function remember(skillId, name) {
    if (player.getSkillLevel(skillId) < 1) {
        player.teachSkill(skillId, 1);
    }
    qm.sendDev("（#b" + name + "#k のスキルを思い出した！　スキルウィンドウで確かめよう。）");
}

// 修行だけが唯一の道！3 (quest 21703, NPC 1202006 プオ, リエン修練所) — 最後の修行: 修練所の 9300343 を 30 体倒す。完了でコンボアビリティ(21000000)を
// 思い出す(Cosmic は teachSkill でマスターレベルを付ける; ここでは Lv1 を渡す [DEV])。EXP 2800 は Cosmic の値(JMS の Act は薬 30 個ずつのみ)。
// 出典 Reference/Cosmic/scripts/quest/21703.js。JMS: 開始 q21703s / 終了 q21703e。台詞は創作。
function start() {
    qm.sendNext("お前の腕、だいぶ形になってきたな。この短い間で、ここまでとは…。");
    qm.sendNext("（そんなに長く鍛えたわけでもないのに、なぜこんなに感心されるんだ…？）");
    qm.sendNext("よし、最後の修行だ。三段階目にして、締めくくりだ。");
    qm.sendNext("（少しは…できる気がする。）");
    qm.sendNext("英雄とは生まれながらの戦士だ。戦うほどに強くなる、飽くなき性分を持っている。");
    qm.sendNext("（本当にそうなのか？）");
    if (!qm.askAccept("いいか。#bもう一度修練所に入って#k、#o9300343# を 30 体倒してこい。それができたら、俺から教えることはもう無い。")) {
        qm.sendOk("相当な力と意志が要るのは分かっている。だが、お前ならできる。準備ができたら来い。");
        return;
    }
    player.startQuest(21703);
    qm.sendOk("さあ行け。あの化け物じみた #o9300343# どもを倒してこい！");
}

function end() {
    qm.sendNext("おお、#o9300343# を 30 体すべて倒して戻ったか。もはや、俺の教えることは何もない…。");
    qm.sendNext("（からかわれているのか…？）");
    if (!qm.askYesNo("お前は俺を超えた。修行はここまでだ。リリンのところへ戻り、報告するといい。いいな？")) {
        qm.sendOk("教官と離れがたいのか？　ぐすっ…俺だって寂しいさ。");
        return;
    }
    player.completeQuest(21703);
    player.gainExp(2800);
    remember(21000000, "コンボアビリティ");
    qm.sendOk("さあ、#p1201000# のところへ戻って報告しろ。きっと大喜びするぞ。");
}
