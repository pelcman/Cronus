using Cronus.Domain;
using Cronus.Network;
using Cronus.Server.Game;

namespace Cronus.Server.Channel;

/// <summary>
/// Job advancement for scripts (ports the oracle's <c>MapleCharacter.changeJob</c>): the job
/// itself, the advancement SP, the job's max HP/MP bonus with a refill, the StatChanged for the
/// client and the JobChanged effect for the field. The oracle also resets the stat distribution
/// on every advancement (its <c>resetStatsByJob(true)</c> call, a private-server convenience —
/// retail v186 kept the stats), so that is the separate <c>resetStatsForJob</c> the Cygnus /
/// Aran first-job scripts call, exactly where Cosmic's scripts call <c>resetStats</c>.
/// Not ported yet: <c>baseSkills</c> (the master-level records of 4th-job skills on reaching a
/// third-tier job) — see TASK.md.
/// </summary>
public sealed partial class ChannelHandler
{
    /// <summary>
    /// The oracle's max HP / MP bonus on advancing into <paramref name="job"/> (a random roll in the
    /// job's range; jobs outside its table — the Cygnus first jobs other than Soul Master, every
    /// third and fourth tier — get nothing).
    /// </summary>
    public static (int Hp, int Mp) JobAdvancementGains(int job, Random? random = null)
    {
        Random r = random ?? Random.Shared;
        int Roll(int min, int max) => r.Next(min, max + 1);
        return job switch
        {
            100 or 1100 or 2100 or 3200 => (Roll(200, 250), 0),
            200 or 2200 or 2210 => (0, Roll(100, 150)),
            300 or 400 or 500 or 3300 or 3500 => (Roll(100, 150), Roll(25, 50)),
            110 => (Roll(300, 350), 0),
            120 or 130 or 1110 or 2110 or 3210 => (Roll(300, 350), 0),
            210 or 220 or 230 => (0, Roll(400, 450)),
            310 or 320 or 410 or 420 or 430 or 1310 or 1410 or 3310 or 3510 => (Roll(300, 350) + Roll(150, 200), 0),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// The SP the oracle grants on advancing into <paramref name="newJob"/> at <paramref name="level"/>:
    /// one for any real job, two more for a fourth-tier job (x2), and for a first job taken late the
    /// three per level the levels past 10 (8 for a magician) would have paid.
    /// </summary>
    public static int JobAdvancementSp(int newJob, int level)
    {
        int sp = 0;
        if (newJob != 0 && newJob != 1000 && newJob != 2000 && newJob != 2001 && newJob != 3000)
        {
            sp += 1;
            if (newJob % 10 >= 2)
            {
                sp += 2;
            }
        }

        if (newJob > 0)
        {
            int firstJobLevel = newJob == 200 ? 8 : 10;
            if (level > firstJobLevel && newJob % 100 == 0 && (newJob % 1000) / 100 > 0)
            {
                sp += 3 * (level - firstJobLevel);
            }
        }

        return sp;
    }

    /// <summary>A script's <c>player.changeJob</c>: the oracle's advancement, told to the client and the field.</summary>
    private void ChangeJobFromScript(MapleSession session, int newJob)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        c.Job = (short)newJob;
        c.Sp = (short)Math.Min(short.MaxValue, c.Sp + JobAdvancementSp(newJob, c.Level));
        (int hp, int mp) = JobAdvancementGains(newJob);
        c.MaxHp = (short)Math.Min(30000, c.MaxHp + hp);
        c.MaxMp = (short)Math.Min(30000, c.MaxMp + mp);
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;
        _characters.Save(c);

        SendBlocking(session, _packets.StatChanged(c, StatFlag.Job | StatFlag.Hp | StatFlag.MaxHp | StatFlag.Mp | StatFlag.MaxMp | StatFlag.Sp));
        if (_field is { } field)
        {
            field.BroadcastAsync(_packets.UserEffectRemote(c.Id, ChannelPackets.UserEffectJobChanged), exceptCharacterId: c.Id)
                .AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// A script's <c>player.resetStatsForJob</c> (ports <c>resetStatsByJob(true)</c> + <c>resetStats</c>):
    /// a first job's base stats — warrior 25/4/4/4, magician 4/4/20/4, bowman and thief 4/25/4/4,
    /// pirate 4/20/4/4 — with every other point returned to AP. Other jobs are left alone.
    /// </summary>
    private void ResetStatsForJobFromScript(MapleSession session)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        (int str, int dex, int intl, int luk) = (c.Job % 1000) switch
        {
            100 => (25, 4, 4, 4),
            200 => (4, 4, 20, 4),
            300 or 400 => (4, 25, 4, 4),
            500 => (4, 20, 4, 4),
            _ => (-1, 0, 0, 0),
        };
        if (str < 0)
        {
            return;
        }

        int total = c.Str + c.Dex + c.Int + c.Luk + c.Ap;
        c.Str = (short)str;
        c.Dex = (short)dex;
        c.Int = (short)intl;
        c.Luk = (short)luk;
        c.Ap = (short)Math.Max(0, total - str - dex - intl - luk);
        _characters.Save(c);
        SendBlocking(session, _packets.StatChanged(c, StatFlag.Str | StatFlag.Dex | StatFlag.Int | StatFlag.Luk | StatFlag.Ap));
    }

    private static void SendBlocking(MapleSession session, byte[] packet)
        => session.SendAsync(packet).AsTask().GetAwaiter().GetResult();
}
