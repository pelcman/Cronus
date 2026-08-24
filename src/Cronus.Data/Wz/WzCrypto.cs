using System.Security.Cryptography;

namespace Cronus.Data.Wz;

/// <summary>
/// The WZ string keystream (ports the read side of MapleLib's <c>WzKeyGenerator</c>, which the
/// wz_xml dump this repo consumed was itself produced with). Strings inside .wz files are XOR'd
/// with a rolling mask (0xAA.. ascii / 0xAAAA.. unicode) plus this keystream; the keystream is
/// AES-256-ECB over the 4-byte version IV repeated to 16 bytes, each block encrypting the last.
/// An all-zero IV means no AES layer (the mask alone), which is what several regions ship.
/// </summary>
public sealed class WzCrypto
{
    /// <summary>The 32-byte AES user key (same TrimmedUserKey as the network layer's skey —
    /// never change Java/client-derived constants; see <c>MapleAESOFB</c>).</summary>
    private static readonly byte[] AesKey =
    {
        0x13, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
        0x06, 0x00, 0x00, 0x00, 0xB4, 0x00, 0x00, 0x00,
        0x1B, 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00, 0x00,
        0x33, 0x00, 0x00, 0x00, 0x52, 0x00, 0x00, 0x00,
    };

    /// <summary>The version IVs seen in the wild; archives are probed against each.</summary>
    public static readonly (string Name, byte[] Iv)[] KnownIvs =
    {
        ("none", new byte[] { 0, 0, 0, 0 }),
        ("gms", new byte[] { 0x4D, 0x23, 0xC7, 0x2B }),
        ("ems", new byte[] { 0xB9, 0x7D, 0x63, 0xE9 }),
    };

    private readonly Aes? _aes;
    private byte[] _keystream = Array.Empty<byte>();

    public WzCrypto(byte[] iv)
    {
        if (iv.All(b => b == 0))
        {
            return; // zero IV = no AES layer; the keystream stays empty (XOR with 0)
        }

        _aes = Aes.Create();
        _aes.KeySize = 256;
        _aes.Key = AesKey;
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;

        _keystream = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            _keystream[i] = iv[i % 4];
        }

        _keystream = _aes.EncryptEcb(_keystream, PaddingMode.None);
    }

    /// <summary>The keystream byte at <paramref name="index"/> (0 when no AES layer).</summary>
    public byte KeyAt(int index)
    {
        if (_aes is null)
        {
            return 0;
        }

        while (index >= _keystream.Length)
        {
            byte[] next = _aes.EncryptEcb(_keystream.AsSpan(^16..).ToArray(), PaddingMode.None);
            byte[] grown = new byte[_keystream.Length + 16];
            _keystream.CopyTo(grown, 0);
            next.CopyTo(grown, _keystream.Length);
            _keystream = grown;
        }

        return _keystream[index];
    }
}
