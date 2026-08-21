// Sample reactor script (place at CRONUS_SCRIPTS/reactor/<reactorId>.js).
// Runs when the reactor breaks; `player` is the breaker (same API as NPC scripts,
// no dialog). 1002008 is the wooden box seen around Victoria hunting maps.
function start() {
    player.gainItem(2000000, 1); // a Red Potion tumbles out
}
