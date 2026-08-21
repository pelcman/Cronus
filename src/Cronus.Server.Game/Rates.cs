namespace Cronus.Server.Game;

/// <summary>
/// Server rate multipliers (ports the channel exp/drop/meso rates): kill exp, drop-table chance,
/// and mob meso amounts are scaled by these. 1.0 = authentic rates.
/// </summary>
public sealed record Rates(double Exp = 1.0, double Drop = 1.0, double Meso = 1.0)
{
    public static readonly Rates Default = new();
}
