// ChannelHandler partial: buddy list, guilds, guild/party chat, gather/sort.
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

public sealed partial class ChannelHandler
{
    /// <summary>
    /// Handles <c>CP_FriendRequest</c> — the buddy list (ports <c>ReqSub_FriendRequest</c>): add a
    /// friend (they get a hidden pending entry + the invite popup), accept (the hidden entry turns
    /// visible on both sides), delete/decline, and reload. Adding is online-only for now (there is
    /// no by-name character lookup for offline players yet).
    /// </summary>
    private async ValueTask HandleFriendRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte flag = packet.ReadByte();
        switch (flag)
        {
            case FriendReqLoad:
                await SendBuddyListAsync(session, c, ChannelPackets.FriendLoadDone).ConfigureAwait(false);
                break;

            case FriendReqSet:
            {
                string name = packet.ReadString();
                string tag = packet.Remaining > 0 ? packet.ReadString() : string.Empty;
                if (tag.Length == 0)
                {
                    tag = ChannelPackets.DefaultBuddyTag;
                }

                if (c.Buddies.Count >= c.BuddyCapacity)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetFullMe)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? target = _fields.FindPlayerByName(name);
                Character? t = target?.Character ?? _characters.FindByName(name);
                if (t is null || t.Id == c.Id)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetUnknownUser)).ConfigureAwait(false);
                    return;
                }

                if (c.Buddies.ContainsKey(t.Id))
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetAlready)).ConfigureAwait(false);
                    return;
                }

                if (t.Buddies.Count >= t.BuddyCapacity)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetFullOther)).ConfigureAwait(false);
                    return;
                }

                // The target gets a hidden pending entry; when online they also get the invite popup
                // now (an offline target sees it on their next login).
                t.Buddies[c.Id] = new BuddyEntry(c.Name, ChannelPackets.DefaultBuddyTag, Hidden: true);
                _characters.Save(t);
                if (target is not null)
                {
                    await TrySendAsync(target, BuildBuddyList(t, ChannelPackets.FriendSetDone)).ConfigureAwait(false);
                    await TrySendAsync(target, _packets.BuddyInvite(c.Id, c.Name, c.Level, c.Job)).ConfigureAwait(false);
                }

                c.Buddies[t.Id] = new BuddyEntry(t.Name, tag, Hidden: false);
                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendSetDone).ConfigureAwait(false);
                break;
            }

            case FriendReqAccept:
            {
                int friendId = packet.ReadInt();
                if (!c.Buddies.TryGetValue(friendId, out BuddyEntry? pending) || !pending.Hidden)
                {
                    return;
                }

                c.Buddies[friendId] = pending with { Hidden = false };
                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendSetDone).ConfigureAwait(false);

                if (FindOnlinePlayer(friendId) is { } friend)
                {
                    await TrySendAsync(friend, BuildBuddyList(friend.Character, ChannelPackets.FriendSetDone)).ConfigureAwait(false);
                }

                break;
            }

            case FriendReqDelete:
            {
                int friendId = packet.ReadInt();
                if (!c.Buddies.Remove(friendId))
                {
                    return;
                }

                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendDeleteDone).ConfigureAwait(false);

                // The other side's entry stays but now shows this player as offline.
                if (FindOnlinePlayer(friendId) is { } friend && friend.Character.Buddies.ContainsKey(c.Id))
                {
                    await TrySendAsync(friend, _packets.BuddyChannelUpdate(c.Id, -1)).ConfigureAwait(false);
                }

                break;
            }
        }
    }

    /// <summary>An online player by character id across the channel's fields, or null.</summary>
    private FieldPlayer? FindOnlinePlayer(int characterId)
    {
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (player.Character.Id == characterId)
                {
                    return player;
                }
            }
        }

        return null;
    }

    private byte[] BuildBuddyList(Character c, byte flag)
    {
        var rows = new List<ChannelPackets.BuddyRow>(c.Buddies.Count);
        foreach ((int id, BuddyEntry entry) in c.Buddies)
        {
            int channel = FindOnlinePlayer(id) is null ? -1 : 0;
            rows.Add(new ChannelPackets.BuddyRow(id, entry.Name, entry.Tag, entry.Hidden, channel));
        }

        return _packets.BuddyListResult(flag, rows);
    }

    private async ValueTask SendBuddyListAsync(MapleSession session, Character c, byte flag)
        => await session.SendAsync(BuildBuddyList(c, flag)).ConfigureAwait(false);

    /// <summary>Tells everyone who lists this player as a buddy that their channel changed.</summary>
    private async ValueTask NotifyBuddiesOfPresenceAsync(int characterId, int channel)
    {
        foreach ((FieldRegistry fields, _) in WorldChannels())
        {
            foreach (Field field in fields.Fields)
            {
                foreach (FieldPlayer player in field.Players)
                {
                    if (player.Character.Id != characterId
                        && player.Character.Buddies.TryGetValue(characterId, out BuddyEntry? entry)
                        && !entry.Hidden)
                    {
                        await TrySendAsync(player, _packets.BuddyChannelUpdate(characterId, channel)).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>Every channel's fields when the world is known, else just this channel's.</summary>
    private IEnumerable<(FieldRegistry Fields, int ChannelId)> WorldChannels()
    {
        if (_worldFields is null || _worldFields.Count == 0)
        {
            yield return (_fields, _channelId);
            yield break;
        }

        for (int i = 0; i < _worldFields.Count; i++)
        {
            yield return (_worldFields[i], i);
        }
    }

    /// <summary>Finds a player by name on any channel; returns them and their channel id.</summary>
    private (FieldPlayer Player, int ChannelId)? FindWorldPlayerByName(string name)
    {
        foreach ((FieldRegistry fields, int channelId) in WorldChannels())
        {
            if (fields.FindPlayerByName(name) is { } player)
            {
                return (player, channelId);
            }
        }

        return null;
    }

    // CP_GuildRequest ops (the reference GuildHandler's raw switch values).
    private const byte GuildReqCreate = 0x02;
    private const byte GuildReqInvite = 0x05;
    private const byte GuildReqJoin = 0x06;
    private const byte GuildReqLeave = 0x07;
    private const byte GuildReqExpel = 0x08;
    private const byte GuildReqRankTitles = 0x0D;
    private const byte GuildReqRankChange = 0x0E;
    private const byte GuildReqEmblem = 0x0F;
    private const byte GuildReqNotice = 0x10;

    /// <summary>The Orbis guild headquarters map, where creation/emblem changes happen.</summary>
    private const int GuildHqMapId = 200000301;
    private const int GuildCreateCost = 5_000_000;
    private const int GuildEmblemCost = 15_000_000;

    /// <summary>
    /// Handles <c>CP_GuildRequest</c> — the guild window (ports <c>GuildHandler.Guild</c>):
    /// create (at the HQ, for meso), invite/join/leave/expel, rank titles and ranks, emblem, and
    /// notice. The leader leaving disbands the guild (same simplification as party leadership).
    /// </summary>
    private async ValueTask HandleGuildRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte op = packet.ReadByte();
        switch (op)
        {
            case GuildReqCreate:
            {
                string name = packet.ReadString();
                if (c.MapId != GuildHqMapId)
                {
                    await session.SendAsync(_packets.BroadcastNotice("ギルドはギルド本部でのみ作成できます。", alert: true)).ConfigureAwait(false);
                    return;
                }

                await CreateGuildAsync(session, c, name, cost: GuildCreateCost).ConfigureAwait(false);
                break;
            }

            case GuildReqInvite:
            {
                if (c.GuildId <= 0 || c.GuildRank > 2) // 1 = master, 2 = jr. master
                {
                    return;
                }

                string name = packet.ReadString();
                FieldPlayer? target = FindOnlinePlayerByName(name);
                if (target is null)
                {
                    await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResTargetOffline)).ConfigureAwait(false);
                }
                else if (target.Character.GuildId > 0)
                {
                    await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResTargetInGuild)).ConfigureAwait(false);
                }
                else
                {
                    _guilds.Invite(target.Character.Name, c.GuildId);
                    await TrySendAsync(target, _packets.GuildInvite(c.GuildId, c.Name, c.Level, c.Job)).ConfigureAwait(false);
                }

                break;
            }

            case GuildReqJoin:
            {
                int guildId = packet.ReadInt();
                int characterId = packet.ReadInt();
                if (characterId != c.Id || c.GuildId > 0 || !_guilds.TakeInvite(c.Name, guildId))
                {
                    return;
                }

                GuildData? guild = _guilds.Get(guildId);
                if (guild is null)
                {
                    return;
                }

                IReadOnlyList<Character> members = _characters.ListByGuild(guildId);
                if (members.Count >= guild.Capacity)
                {
                    await session.SendAsync(_packets.BroadcastNotice("そのギルドは満員です。", alert: true)).ConfigureAwait(false);
                    return;
                }

                c.GuildId = guildId;
                c.GuildRank = 5;
                _characters.Save(c);
                _guilds.SetOnline(guildId, _player);

                var row = new ChannelPackets.GuildMemberRow(c.Id, c.Name, c.Job, c.Level, c.GuildRank, Online: true);
                await BroadcastToGuildAsync(guildId, _packets.GuildNewMember(guildId, row)).ConfigureAwait(false);
                await session.SendAsync(_packets.GuildInfo(guild, BuildGuildMembers(guildId))).ConfigureAwait(false);
                break;
            }

            case GuildReqLeave:
            {
                int characterId = packet.ReadInt();
                string name = packet.ReadString();
                if (characterId != c.Id || !string.Equals(name, c.Name, StringComparison.Ordinal) || c.GuildId <= 0)
                {
                    return;
                }

                if (_guilds.Get(c.GuildId) is { } guild && guild.LeaderId == c.Id)
                {
                    await DisbandGuildAsync(guild).ConfigureAwait(false);
                }
                else
                {
                    int guildId = c.GuildId;
                    await BroadcastToGuildAsync(guildId, _packets.GuildMemberLeft(guildId, c.Id, c.Name, expelled: false)).ConfigureAwait(false);
                    c.GuildId = 0;
                    c.GuildRank = 0;
                    _characters.Save(c);
                    _guilds.SetOffline(guildId, c.Id);
                    await session.SendAsync(_packets.GuildInfoNone()).ConfigureAwait(false);
                }

                break;
            }

            case GuildReqExpel:
            {
                int characterId = packet.ReadInt();
                packet.ReadString(); // the claimed name; the server uses the repo's record
                if (c.GuildId <= 0 || c.GuildRank > 2)
                {
                    return;
                }

                Character? target = _characters.Find(characterId);
                if (target is null || target.GuildId != c.GuildId || target.Id == c.Id)
                {
                    return;
                }

                int guildId = c.GuildId;
                await BroadcastToGuildAsync(guildId, _packets.GuildMemberLeft(guildId, target.Id, target.Name, expelled: true)).ConfigureAwait(false);
                target.GuildId = 0;
                target.GuildRank = 0;
                _characters.Save(target);
                if (FindOnlinePlayer(target.Id) is { } online)
                {
                    await TrySendAsync(online, _packets.GuildInfoNone()).ConfigureAwait(false);
                }

                _guilds.SetOffline(guildId, target.Id);
                break;
            }

            case GuildReqRankTitles:
            {
                if (_guilds.Get(c.GuildId) is not { } guild || guild.LeaderId != c.Id)
                {
                    return;
                }

                var titles = new List<string>(5);
                for (int i = 0; i < 5; i++)
                {
                    titles.Add(packet.ReadString());
                }

                guild.RankTitles = titles;
                _guilds.Save(guild);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildRankTitles(guild.Id, titles)).ConfigureAwait(false);
                break;
            }

            case GuildReqRankChange:
            {
                int characterId = packet.ReadInt();
                byte newRank = packet.ReadByte();

                // Ports the reference gates: only 2..5 assignable, jr+ may demote/promote, and
                // ranks 2 and below are the master's alone to grant.
                if (newRank is <= 1 or > 5 || c.GuildRank > 2 || (newRank <= 2 && c.GuildRank != 1) || c.GuildId <= 0)
                {
                    return;
                }

                Character? target = _characters.Find(characterId);
                if (target is null || target.GuildId != c.GuildId)
                {
                    return;
                }

                target.GuildRank = newRank;
                _characters.Save(target);
                await BroadcastToGuildAsync(c.GuildId, _packets.GuildMemberRankChanged(c.GuildId, target.Id, newRank)).ConfigureAwait(false);
                break;
            }

            case GuildReqEmblem:
            {
                if (_guilds.Get(c.GuildId) is not { } guild || guild.LeaderId != c.Id || c.MapId != GuildHqMapId)
                {
                    return;
                }

                if (c.Meso < GuildEmblemCost)
                {
                    await session.SendAsync(_packets.BroadcastNotice("メルが足りません。", alert: true)).ConfigureAwait(false);
                    return;
                }

                guild.LogoBG = packet.ReadShort();
                guild.LogoBGColor = packet.ReadByte();
                guild.Logo = packet.ReadShort();
                guild.LogoColor = packet.ReadByte();
                _guilds.Save(guild);

                c.Meso -= GuildEmblemCost;
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildEmblemChanged(guild.Id, guild.LogoBG, guild.LogoBGColor, guild.Logo, guild.LogoColor)).ConfigureAwait(false);
                break;
            }

            case GuildReqNotice:
            {
                string notice = packet.ReadString();
                if (notice.Length > 100 || c.GuildId <= 0 || c.GuildRank > 2)
                {
                    return;
                }

                if (_guilds.Get(c.GuildId) is not { } guild)
                {
                    return;
                }

                guild.Notice = notice;
                _guilds.Save(guild);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildNotice(guild.Id, notice)).ConfigureAwait(false);
                break;
            }
        }
    }

    /// <summary>
    /// Creates a guild with this player as leader (rank 1); <paramref name="cost"/> is deducted
    /// (0 for the free <c>/guildcreate</c> command). Shared by the client's HQ flow and the command.
    /// </summary>
    private async ValueTask CreateGuildAsync(MapleSession session, Character c, string name, int cost)
    {
        if (c.GuildId > 0 || name.Length is < 1 or > 12)
        {
            return;
        }

        if (_guilds.FindByName(name) is not null)
        {
            await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResNameInUse)).ConfigureAwait(false);
            return;
        }

        if (cost > 0 && c.Meso < cost)
        {
            await session.SendAsync(_packets.BroadcastNotice("メルが足りません。", alert: true)).ConfigureAwait(false);
            return;
        }

        GuildData guild = _guilds.Create(name, c.Id);
        c.GuildId = guild.Id;
        c.GuildRank = 1;
        if (cost > 0)
        {
            c.Meso -= cost;
        }

        _characters.Save(c);
        _guilds.SetOnline(guild.Id, _player!);

        if (cost > 0)
        {
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.GuildInfo(guild, BuildGuildMembers(guild.Id))).ConfigureAwait(false);
    }

    /// <summary>Disbands a guild: every member (online or not) becomes guildless.</summary>
    private async ValueTask DisbandGuildAsync(GuildData guild)
    {
        // Mutate state first so a member reacting to the packet can't observe the old guild.
        IReadOnlyCollection<FieldPlayer> online = _guilds.OnlineMembers(guild.Id);
        foreach (Character member in _characters.ListByGuild(guild.Id))
        {
            member.GuildId = 0;
            member.GuildRank = 0;
            _characters.Save(member);
        }

        _guilds.Delete(guild.Id); // also clears the online roster

        foreach (FieldPlayer member in online)
        {
            await TrySendAsync(member, _packets.GuildDisband(guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_GuildResult</c> — declining a guild invitation (ports
    /// <c>GuildHandler.DenyGuildRequest</c>): the original inviter is told who declined.
    /// </summary>
    private async ValueTask HandleGuildDenyAsync(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadByte(); // mode
        string inviterName = packet.ReadString();
        if (FindOnlinePlayerByName(inviterName) is { } inviter)
        {
            await TrySendAsync(inviter, _packets.GuildInviteDenied(_player.Character.Name)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_GroupMessage</c> — friend / party / guild chat (ports
    /// <c>ReqCUser.OnGroupMessage</c>): relays the line to the group's other online members via
    /// <c>LP_GroupMessage</c>. Friend chat targets the ids the client listed (gated on the buddy
    /// list); party and guild membership come from the server's own registries.
    /// </summary>
    private async ValueTask HandleGroupMessageAsync(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte chatTarget = packet.ReadByte();
        int memberCount = packet.ReadByte();
        var memberIds = new int[memberCount];
        for (int i = 0; i < memberCount; i++)
        {
            memberIds[i] = packet.ReadInt();
        }

        string text = packet.ReadString();

        switch (chatTarget)
        {
            case ChannelPackets.ChatGroupFriend:
                foreach (int id in memberIds)
                {
                    if (id != c.Id
                        && FindOnlinePlayer(id) is { } friend
                        && friend.Character.Buddies.TryGetValue(c.Id, out BuddyEntry? entry)
                        && !entry.Hidden)
                    {
                        await TrySendAsync(friend, _packets.GroupMessage(ChannelPackets.ChatGroupFriend, c.Name, text)).ConfigureAwait(false);
                    }
                }

                break;

            case ChannelPackets.ChatGroupParty:
                if (_parties.GetForCharacter(c.Id) is { } party)
                {
                    foreach (FieldPlayer member in party.Members)
                    {
                        if (member.Character.Id != c.Id)
                        {
                            await TrySendAsync(member, _packets.GroupMessage(ChannelPackets.ChatGroupParty, c.Name, text)).ConfigureAwait(false);
                        }
                    }
                }

                break;

            case ChannelPackets.ChatGroupGuild:
                if (c.GuildId > 0)
                {
                    await BroadcastToGuildAsync(c.GuildId, _packets.GroupMessage(ChannelPackets.ChatGroupGuild, c.Name, text), exceptCharacterId: c.Id).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>The wire member table for a guild, derived from the character store.</summary>
    private List<ChannelPackets.GuildMemberRow> BuildGuildMembers(int guildId)
    {
        var rows = new List<ChannelPackets.GuildMemberRow>();
        foreach (Character m in _characters.ListByGuild(guildId))
        {
            rows.Add(new ChannelPackets.GuildMemberRow(
                m.Id, m.Name, m.Job, m.Level, m.GuildRank, FindOnlinePlayer(m.Id) is not null));
        }

        return rows;
    }

    /// <summary>Sends a packet to every online guild member (optionally excluding one).</summary>
    private async ValueTask BroadcastToGuildAsync(int guildId, byte[] packet, int exceptCharacterId = -1)
    {
        foreach (FieldPlayer member in _guilds.OnlineMembers(guildId))
        {
            if (member.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(member, packet).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles <c>CP_UserGatherItemRequest</c> — the inventory "gather" button (ports
    /// <c>OnUserGatherItemRequest</c>): compacts the tab and relays the moves + the ack.
    /// </summary>
    private async ValueTask HandleGatherItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt(); // timestamp
        byte tab = packet.ReadByte();
        List<InventoryChange> changes = Inventory.Gather(_player.Character, tab);
        if (changes.Count > 0)
        {
            _characters.Save(_player.Character);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.GatherItemResult(tab)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserSortItemRequest</c> — the inventory "sort" button (ports
    /// <c>OnUserSortItemRequest</c>): selection-sorts the tab by item id and relays the swap moves.
    /// </summary>
    private async ValueTask HandleSortItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt(); // timestamp
        byte tab = packet.ReadByte();
        List<InventoryChange> changes = Inventory.Sort(_player.Character, tab);
        if (changes.Count > 0)
        {
            _characters.Save(_player.Character);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.SortItemResult(tab)).ConfigureAwait(false);
    }
}
