// Sample NPC script (place at CRONUS_SCRIPTS/npc/<npcId>.js).
// Globals: `cm` (conversation) and `player`.
//   cm.sendNext(text) / cm.sendPrev / cm.sendNextPrev / cm.sendOk(text)
//   cm.askYesNo(text) -> bool
//   cm.askMenu(text)  -> selected index (use #Ln#label#l markup in text)
//   cm.askText(text)  -> string
//   cm.dispose()      -> end the conversation
//   player.getName() / getLevel() / getMapId() / getMeso() / gainMeso(n)

function start() {
    var choice = cm.askMenu(
        "Welcome to Cronus, " + player.getName() + "! What can I do for you?\r\n" +
        "#L0#Tell me about this server.#l\r\n" +
        "#L1#Spare some mesos?#l\r\n" +
        "#L2#Nothing, thanks.#l");

    if (choice == 0) {
        cm.sendNext("Cronus is an open-source JMS v186 server written in C#.");
        cm.sendOk("Enjoy your stay!");
    } else if (choice == 1) {
        player.gainMeso(1000);
        cm.sendOk("Here's 1000 mesos! You now have " + player.getMeso() + ".");
    } else {
        cm.sendOk("Come back any time.");
    }
}
