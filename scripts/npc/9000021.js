// Example: a travel + healer NPC. Demonstrates player.heal() and player.warp().
// Map ids below are the classic JMS towns — adjust to whatever your wz has.
function start() {
    var HENESYS = 100000000;
    var ELLINIA = 101000000;
    var PERION  = 102000000;

    var choice = cm.askMenu(
        "Hello " + player.getName() + "! Where would you like to go? (or rest here)\r\n" +
        "#L0#Heal me up.#l\r\n" +
        "#L1#Henesys#l\r\n" +
        "#L2#Ellinia#l\r\n" +
        "#L3#Perion#l");

    if (choice == 0) {
        player.heal();
        cm.sendOk("There you go — full HP and MP. (" + player.getHp() + "/" + player.getMaxHp() + ")");
    } else if (choice == 1) {
        player.warp(HENESYS);
    } else if (choice == 2) {
        player.warp(ELLINIA);
    } else if (choice == 3) {
        player.warp(PERION);
    }
}
