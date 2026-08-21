// Example: a first-job instructor. Demonstrates getJob/getLevel/setJob/gainSp.
// A beginner (job 0) at level 10+ can advance to Warrior (job 100).
function start() {
    if (player.getJob() != 0) {
        cm.sendOk("You've already chosen your path, " + player.getName() + ".");
        return;
    }
    if (player.getLevel() < 10) {
        cm.sendOk("Come back when you've reached level 10. You're only " + player.getLevel() + ".");
        return;
    }

    if (cm.askYesNo("You have the makings of a Warrior. Shall I make it so?")) {
        player.setJob(100);   // Warrior
        player.gainSp(1);
        cm.sendOk("Rise, Warrior! Spend your SP wisely.");
    } else {
        cm.sendOk("Take your time. The path will wait.");
    }
}
