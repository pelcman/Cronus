using Cronus.Network;
using Cronus.Server.Game;

namespace Cronus.Server.Channel;

/// <summary>
/// Mini dungeons (the wz <c>MD_*</c> portals: 豚農場, キノコ栽培洞窟, …). JMS v186 ships each dungeon as
/// one real map plus a run of empty placeholder copies (no mobs, no portals) that the live server
/// clones into; Cronus has no field instancing yet, so a dungeon is its one real map: free when
/// nobody is inside, shared when the people inside are the player's own party, otherwise refused
/// (the portal script says so with a <c>[DEV]</c> notice). Cosmic's leader-only rule and party warp
/// are folded into "a party member inside lets the rest follow".
/// </summary>
public sealed partial class ChannelHandler
{
    /// <summary>True when <paramref name="inside"/> is empty or contains a member of <paramref name="party"/>.</summary>
    public static bool MiniDungeonAdmits(IReadOnlyList<FieldPlayer> inside, Party? party)
    {
        if (inside.Count == 0)
        {
            return true;
        }

        if (party is null)
        {
            return false;
        }

        HashSet<int> members = party.Members.Select(m => m.Character.Id).ToHashSet();
        return inside.Any(p => members.Contains(p.Character.Id));
    }

    /// <summary>A script's <c>player.enterMiniDungeon</c>: admit, then warp to the dungeon's <c>out00</c> portal.</summary>
    private bool TryEnterMiniDungeon(MapleSession session, int dungeonMapId)
    {
        if (_player is null)
        {
            return false;
        }

        Field dungeon = _fields.Get(dungeonMapId);
        if (!MiniDungeonAdmits(dungeon.Players, _parties.GetForCharacter(_player.Character.Id)))
        {
            return false;
        }

        int portal = _maps.GetMap(dungeonMapId)?.FindPortal("out00")?.Id ?? 0;
        MovePlayerToMapAsync(session, dungeonMapId, portal).AsTask().GetAwaiter().GetResult();
        return true;
    }
}
