// Sample quest script (place at CRONUS_SCRIPTS/quest/<questId>.js).
// A quest whose wz Check declares a script runs this instead of the data-driven accept/complete:
//   function start() — the player clicks the quest's opening (accept) dialog
//   function end()   — the player clicks the quest's completion dialog
// Globals: `qm` (the conversation — same API as an NPC script's `cm`) and `player`.
// Extra player APIs useful in quests:
//   player.gainItem(itemId, n)  — give items (negative n takes them)
//   player.haveItem(itemId)     — true if carrying at least one
//   player.itemQuantity(itemId) — total carried across stacks
//   player.startQuest(id) / completeQuest(id) / hasQuest(id) / isQuestDone(id)

function start() {
    if (qm.askYesNo("Would you help me gather three apples?")) {
        player.startQuest(1000);
        qm.sendOk("Wonderful! Bring me 3 #t2010000#s.");
    } else {
        qm.sendOk("Come back if you change your mind.");
    }
}

function end() {
    if (player.itemQuantity(2010000) < 3) {
        qm.sendOk("You don't have the 3 #t2010000#s yet.");
        return;
    }

    player.gainItem(2010000, -3);
    player.gainExp(100);
    player.completeQuest(1000);
    qm.sendOk("Thank you! Take this experience as a reward.");
}
