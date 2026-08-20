namespace Cronus.Scripting;

/// <summary>
/// The player surface exposed to NPC scripts as the global <c>player</c>. Methods are named in
/// the OdinMS style (getX / gainX) so existing scripts read naturally. Kept intentionally small
/// and read-mostly; field-mutating actions (warp, spawn) are added as the channel gains safe
/// hooks for them.
/// </summary>
public interface INpcPlayer
{
    string getName();

    int getLevel();

    int getMapId();

    int getMeso();

    /// <summary>Adds (or, if negative, removes) mesos, clamped at zero, and persists.</summary>
    void gainMeso(int amount);
}
