using Cronus.Domain;

namespace Cronus.Server.Login;

/// <summary>
/// Per-connection login-stage state, stored on <c>MapleSession.UserData</c>. Tracks the
/// authenticated account and the world/channel the player picked before character select.
/// </summary>
public sealed class LoginState
{
    public required Account Account { get; init; }

    public int SelectedWorld { get; set; } = -1;

    public int SelectedChannel { get; set; } = -1;
}
