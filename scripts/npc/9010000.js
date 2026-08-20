// Sample NPC script (place at CRONUS_SCRIPTS/npc/<npcId>.js).
// The conversation manager is exposed as the global `cm`; `player` is the character.
// cm methods block until the client answers, so scripts read as linear code:
//   cm.sendNext(text) / cm.sendPrev / cm.sendNextPrev / cm.sendOk(text)
//   cm.askYesNo(text) -> bool
//   cm.askMenu(text)  -> selected index (use #Ln#label#l markup in text)
//   cm.askText(text)  -> string
//   cm.dispose()      -> end the conversation

function start() {
    var choice = cm.askMenu(
        "Welcome to Cronus! What can I do for you?\r\n" +
        "#L0#Tell me about this server.#l\r\n" +
        "#L1#Nothing, thanks.#l");

    if (choice == 0) {
        cm.sendNext("Cronus is an open-source JMS v186 server written in C#.");
        cm.sendOk("Enjoy your stay!");
    } else {
        cm.sendOk("Come back any time.");
    }
}
