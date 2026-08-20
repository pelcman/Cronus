using System.Text;

namespace Cronus.Common;

/// <summary>
/// Immutable server profile pinning the protocol variant. Cronus targets a single
/// combination — JMS v186 — but the values that the network core branches on are
/// gathered here rather than hard-coded across the codebase, mirroring the role of
/// upstream <c>tacos.config.Config</c> / <c>Content</c>.
/// </summary>
public sealed class ServerConfig
{
    public required Region Region { get; init; }

    /// <summary>Client version, e.g. 186.</summary>
    public required int Version { get; init; }

    /// <summary>Sub-version; sent as a string in the Hello packet. JMS uses 0 ("0").</summary>
    public int SubVersion { get; init; }

    /// <summary>Wire string encoding. JMS = MS932 (Shift-JIS).</summary>
    public required Encoding CodePage { get; init; }

    /// <summary>Opcode header width in bytes. JMS = 2.</summary>
    public int PacketHeaderSize { get; init; } = 2;

    /// <summary>
    /// Whether the AES-OFB keystream seeds from the old 4x-repeat scheme.
    /// True for JMS &lt;= v141; false (multiplyBytes) for v186.
    /// </summary>
    public bool OldIv { get; init; }

    /// <summary>
    /// Whether the extra "shanda" custom encryption layer applies. False for JMS.
    /// </summary>
    public bool CustomEncryption { get; init; }

    /// <summary>The canonical JMS v186 profile Cronus targets.</summary>
    public static ServerConfig Jms186 { get; } = new()
    {
        Region = Region.Jms,
        Version = 186,
        SubVersion = 0,
        CodePage = CodePage_.Jms(),
        PacketHeaderSize = 2,
        OldIv = false,          // 186 > 141
        CustomEncryption = false, // JMS does not use shanda
    };

    private static class CodePage_
    {
        internal static Encoding Jms() => Common.CodePage.Get("shift_jis");
    }
}
