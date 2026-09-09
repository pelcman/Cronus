// エリン森道しるべ (Ellin Forest signpost, NPC 2133002) — インスタンスから出る。持っている魔石・玉を回収して
// 森出口(930000800)へ。出典 Reference/Cosmic/scripts/npc/2133002.js を JMS v186 に移植。台詞は創作。
// 回収アイテム 4001163 紫色の魔石 / 4001169 モンスター玉 / 2270004 浄化の玉、行き先はいずれも JMS で確認済み。
// Cosmic の removeAll は Cronus に無いため、所持数ぶんを gainItem のマイナスで回収する(インベントリ内のみ、装備は対象外で問題なし)。
function start() {
    if (cm.askYesNo("この空間から出ますか？　パーティーメンバーも中断することになるかもしれません。よろしいですか？")) {
        player.gainItem(4001163, -player.itemQuantity(4001163));
        player.gainItem(4001169, -player.itemQuantity(4001169));
        player.gainItem(2270004, -player.itemQuantity(2270004));
        player.warp(930000800, 0);
    }
}
