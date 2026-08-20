namespace Cronus.Common;

/// <summary>
/// MapleStory regional client family. Cronus targets <see cref="Jms"/> only, but the
/// enumeration mirrors the upstream <c>tacos.config.Region</c> so protocol branches that
/// key off region stay legible. The numeric <see cref="Code"/> is the value sent in the
/// Hello packet's trailing region byte.
/// </summary>
public enum Region
{
    /// <summary>Korea.</summary>
    Kms = 1,

    /// <summary>Japan. Cronus' target.</summary>
    Jms = 3,

    /// <summary>China.</summary>
    Cms = 4,

    /// <summary>Taiwan.</summary>
    Twms = 5,

    /// <summary>Global (English).</summary>
    Gms = 8,
}

public static class RegionExtensions
{
    /// <summary>Region byte written at the end of the Hello handshake packet.</summary>
    public static byte Code(this Region region) => (byte)region;

    /// <summary>
    /// Text encoding used for length-prefixed strings on the wire. JMS uses MS932 (Shift-JIS).
    /// Callers must have registered <c>CodePagesEncodingProvider.Instance</c> first
    /// (see <see cref="CodePage.Register"/>).
    /// </summary>
    public static string CodePageName(this Region region) => region switch
    {
        Region.Jms => "shift_jis", // MS932
        Region.Kms => "ms949",
        Region.Cms => "gb2312",
        Region.Twms => "big5",
        _ => "latin1",
    };
}
