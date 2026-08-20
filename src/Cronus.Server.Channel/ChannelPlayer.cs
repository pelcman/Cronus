using Cronus.Domain;
using Cronus.Network;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// Adapts the in-game character to the scripting layer's <see cref="INpcPlayer"/>. Mutations
/// (gainMeso) update the character, persist through the repository, and push an
/// <c>LP_StatChanged</c> so the client's UI reflects the change immediately. Called from the
/// script's worker thread; the async send is awaited synchronously (the worker thread is
/// dedicated to one conversation, and the session serializes its own writes).
/// </summary>
public sealed class ChannelPlayer : INpcPlayer
{
    private readonly Character _character;
    private readonly ICharacterRepository _characters;
    private readonly MapleSession _session;
    private readonly ChannelPackets _packets;

    public ChannelPlayer(
        Character character,
        ICharacterRepository characters,
        MapleSession session,
        ChannelPackets packets)
    {
        _character = character;
        _characters = characters;
        _session = session;
        _packets = packets;
    }

    public string getName() => _character.Name;

    public int getLevel() => _character.Level;

    public int getMapId() => _character.MapId;

    public int getMeso() => _character.Meso;

    public int getHp() => _character.Hp;

    public int getMaxHp() => _character.MaxHp;

    public int getExp() => _character.Exp;

    public void gainMeso(int amount)
    {
        long updated = (long)_character.Meso + amount;
        _character.Meso = (int)Math.Clamp(updated, 0, int.MaxValue);
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Meso));
    }

    public void gainExp(int amount)
    {
        StatFlag changed = CharacterProgression.GainExp(_character, amount); // processes level-ups
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, changed));
    }

    public void heal()
    {
        _character.Hp = _character.MaxHp;
        _character.Mp = _character.MaxMp;
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Hp | StatFlag.Mp));
    }

    private void Send(byte[] packet)
        => _session.SendAsync(packet).AsTask().GetAwaiter().GetResult();
}
