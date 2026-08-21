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
    private readonly Func<int, int, ValueTask>? _warp;
    private readonly Func<int, ValueTask>? _openShop;
    private readonly Func<ValueTask>? _openStorage;
    private readonly Func<int, int, ValueTask>? _gainItem;
    private readonly Func<int, int>? _itemCount;

    public ChannelPlayer(
        Character character,
        ICharacterRepository characters,
        MapleSession session,
        ChannelPackets packets,
        Func<int, int, ValueTask>? warp = null,
        Func<int, ValueTask>? openShop = null,
        Func<ValueTask>? openStorage = null,
        Func<int, int, ValueTask>? gainItem = null,
        Func<int, int>? itemCount = null)
    {
        _character = character;
        _characters = characters;
        _session = session;
        _packets = packets;
        _warp = warp;
        _openShop = openShop;
        _openStorage = openStorage;
        _gainItem = gainItem;
        _itemCount = itemCount;
    }

    public string getName() => _character.Name;

    public int getLevel() => _character.Level;

    public int getMapId() => _character.MapId;

    public int getMeso() => _character.Meso;

    public int getHp() => _character.Hp;

    public int getMaxHp() => _character.MaxHp;

    public int getExp() => _character.Exp;

    public int getGender() => _character.Gender;

    public int getJob() => _character.Job;

    public int getStr() => _character.Str;

    public int getDex() => _character.Dex;

    public int getInt() => _character.Int;

    public int getLuk() => _character.Luk;

    public int getFame() => _character.Fame;

    public int getAp() => _character.Ap;

    public int getSp() => _character.Sp;

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

    public void warp(int mapId) => warp(mapId, 0);

    /// <summary>
    /// Warps the player via the channel's map-transfer path. Runs synchronously on the script
    /// thread (like <see cref="Send"/>); safe because the client is modal during an NPC dialog, so
    /// no field-mutating packet is being handled concurrently, and the transfer's own operations are
    /// individually thread-safe.
    /// </summary>
    public void warp(int mapId, int portal)
    {
        if (_warp is null)
        {
            return;
        }

        _warp(mapId, portal).AsTask().GetAwaiter().GetResult();
    }

    public void gainAp(int amount)
    {
        _character.Ap = (short)Math.Clamp(_character.Ap + amount, 0, short.MaxValue);
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Ap));
    }

    public void gainSp(int amount)
    {
        _character.Sp = (short)Math.Clamp(_character.Sp + amount, 0, short.MaxValue);
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Sp));
    }

    public void gainFame(int amount)
    {
        _character.Fame = (short)Math.Clamp(_character.Fame + amount, -30000, 30000);
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Fame));
    }

    public void setJob(int job)
    {
        _character.Job = (short)job;
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Job));
    }

    public void gainMaxHp(int amount)
    {
        _character.MaxHp = (short)Math.Clamp(_character.MaxHp + amount, 1, 30000);
        _character.Hp = _character.MaxHp;
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Hp | StatFlag.MaxHp));
    }

    public void gainMaxMp(int amount)
    {
        _character.MaxMp = (short)Math.Clamp(_character.MaxMp + amount, 1, 30000);
        _character.Mp = _character.MaxMp;
        _characters.Save(_character);
        Send(_packets.StatChanged(_character, StatFlag.Mp | StatFlag.MaxMp));
    }

    public bool hasQuest(int questId) => _character.StartedQuests.ContainsKey(questId);

    public bool isQuestDone(int questId) => _character.CompletedQuests.ContainsKey(questId);

    public void startQuest(int questId)
    {
        _character.StartedQuests[questId] = string.Empty;
        _characters.Save(_character);
        Send(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordStarted)); // journal updates live
    }

    public void completeQuest(int questId)
    {
        _character.StartedQuests.Remove(questId);
        _character.CompletedQuests[questId] = CharacterDataEncoder.FileTimeNow();
        _characters.Save(_character);
        Send(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordCompleted));
        Send(_packets.UserEffectLocal(ChannelPackets.UserEffectQuestComplete)); // the completion jingle
    }

    public void gainItem(int itemId, int quantity)
        => _gainItem?.Invoke(itemId, quantity).AsTask().GetAwaiter().GetResult();

    public bool haveItem(int itemId) => itemQuantity(itemId) > 0;

    public int itemQuantity(int itemId) => _itemCount?.Invoke(itemId) ?? 0;

    public void openShop(int shopId)
        => _openShop?.Invoke(shopId).AsTask().GetAwaiter().GetResult();

    public void openStorage()
        => _openStorage?.Invoke().AsTask().GetAwaiter().GetResult();

    private void Send(byte[] packet)
        => _session.SendAsync(packet).AsTask().GetAwaiter().GetResult();
}
