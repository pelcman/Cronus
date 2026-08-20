using Cronus.Domain;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// Adapts the in-game character to the scripting layer's <see cref="INpcPlayer"/>. Mutations
/// (gainMeso) update the character and persist through the repository. Called from the script's
/// worker thread; the operations here are on the character object + repository only (no session
/// or field state), so they are safe to run off the network thread.
/// </summary>
public sealed class ChannelPlayer : INpcPlayer
{
    private readonly Character _character;
    private readonly ICharacterRepository _characters;

    public ChannelPlayer(Character character, ICharacterRepository characters)
    {
        _character = character;
        _characters = characters;
    }

    public string getName() => _character.Name;

    public int getLevel() => _character.Level;

    public int getMapId() => _character.MapId;

    public int getMeso() => _character.Meso;

    public void gainMeso(int amount)
    {
        long updated = (long)_character.Meso + amount;
        _character.Meso = (int)Math.Clamp(updated, 0, int.MaxValue);
        _characters.Save(_character);
    }
}
