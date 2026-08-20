using System.Text;

namespace Cronus.Common;

/// <summary>
/// Helper for the legacy code pages MapleStory uses on the wire (Shift-JIS for JMS).
/// .NET Core does not ship these by default; the code-pages provider must be registered
/// once at process start.
/// </summary>
public static class CodePage
{
    private static bool _registered;
    private static readonly object Gate = new();

    /// <summary>
    /// Registers <see cref="CodePagesEncodingProvider"/> so encodings such as
    /// <c>shift_jis</c> / <c>ms932</c> resolve. Idempotent and thread-safe.
    /// Call this before the first packet is serialized.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _registered = true;
        }
    }

    /// <summary>Resolves an <see cref="Encoding"/> by name, registering the provider first.</summary>
    public static Encoding Get(string name)
    {
        Register();
        return Encoding.GetEncoding(name);
    }
}
