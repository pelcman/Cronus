using System.Security.Cryptography;

namespace Cronus.Network.Crypto;

/// <summary>
/// MapleStory AES-OFB packet cipher for one direction of a session. Ports
/// <c>tacos.network.MapleAESOFB</c> for the JMS v186 branch: AES-256 in a home-grown
/// OFB construction, a 4-byte IV that advances per packet, and the 4-byte length header
/// generation / validation used for framing.
///
/// One instance is stateful and single-threaded: it owns a mutable 4-byte IV that both
/// <see cref="Crypt"/> reads and <see cref="AdvanceIv"/> mutates. Create one cipher per
/// direction (send / receive) per session.
/// </summary>
public sealed class AesOfbCipher : IDisposable
{
    // First keystream block starts fresh each 0x5B0 (then 0x5B4) byte segment.
    private const int FirstBlockLength = 0x5B0;
    private const int NextBlockLength = 0x5B4;

    private readonly Aes _aes;
    private readonly byte[] _iv = new byte[4];
    private readonly byte[] _keystream = new byte[16];
    private readonly byte[] _keystreamNext = new byte[16];

    /// <summary>16-bit version marker folded into the framing header.</summary>
    private readonly ushort _mapleVersion;

    /// <summary>
    /// Creates a cipher for one direction.
    /// </summary>
    /// <param name="iv">Initial 4-byte IV.</param>
    /// <param name="version">Client version (e.g. 186).</param>
    /// <param name="outbound">
    /// True for the server→client (send) direction, which marks the version as
    /// <c>0xFFFF - version</c>; false for the receive direction, which uses the raw version.
    /// </param>
    public AesOfbCipher(ReadOnlySpan<byte> iv, int version, bool outbound)
    {
        if (iv.Length != 4)
        {
            throw new ArgumentException("IV must be exactly 4 bytes.", nameof(iv));
        }

        iv.CopyTo(_iv);

        int marker = outbound ? (0xFFFF - version) : version;
        _mapleVersion = ByteSwap16((ushort)marker);

        _aes = Aes.Create();
        _aes.Key = MapleCrypto.DefaultAesKey;
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
    }

    /// <summary>Current 4-byte IV (copy).</summary>
    public byte[] Iv => _iv.ToArray();

    /// <summary>
    /// Encrypts or decrypts <paramref name="data"/> in place (the transform is symmetric).
    /// Does NOT advance the IV — call <see cref="AdvanceIv"/> once per packet afterwards,
    /// matching the upstream encoder/decoder flow.
    /// </summary>
    public void Crypt(Span<byte> data)
    {
        int remaining = data.Length;
        int blockLength = FirstBlockLength;
        int start = 0;

        while (remaining > 0)
        {
            SeedKeystream();
            if (remaining < blockLength)
            {
                blockLength = remaining;
            }

            for (int x = start; x < start + blockLength; x++)
            {
                int j = (x - start) & 15;
                if (j == 0)
                {
                    NextKeystreamBlock();
                }

                data[x] ^= _keystream[j];
            }

            start += blockLength;
            remaining -= blockLength;
            blockLength = NextBlockLength;
        }
    }

    /// <summary>Advances the IV to the next-packet value (upstream <c>updateIv</c>).</summary>
    public void AdvanceIv() => MapleCrypto.NextIv(_iv);

    /// <summary>
    /// Writes the 4-byte framing header for a body of <paramref name="length"/> bytes into
    /// <paramref name="dest"/>. Uses the current IV; does not advance it.
    /// </summary>
    public void WriteHeader(Span<byte> dest, int length)
    {
        if (dest.Length < 4)
        {
            throw new ArgumentException("Header destination must be at least 4 bytes.", nameof(dest));
        }

        int iiv = ((_iv[3] & 0xFF) | ((_iv[2] << 8) & 0xFF00)) ^ _mapleVersion;
        int swappedLen = ((length << 8) & 0xFF00) | ((length >> 8) & 0xFF);
        int mlength = swappedLen ^ iiv;

        dest[0] = (byte)((iiv >> 8) & 0xFF);
        dest[1] = (byte)(iiv & 0xFF);
        dest[2] = (byte)((mlength >> 8) & 0xFF);
        dest[3] = (byte)(mlength & 0xFF);
    }

    /// <summary>Recovers the body length encoded in a 4-byte framing header.</summary>
    public static int ReadLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
        {
            throw new ArgumentException("Header must be at least 4 bytes.", nameof(header));
        }

        int iiv = ((header[0] & 0xFF) << 8) | (header[1] & 0xFF);
        int mlength = ((header[2] & 0xFF) << 8) | (header[3] & 0xFF);
        int swapped = iiv ^ mlength;
        return ((swapped << 8) & 0xFF00) | ((swapped >> 8) & 0xFF);
    }

    /// <summary>
    /// Validates that a 4-byte header was produced against this (receive) cipher's current
    /// IV and version. Mirrors upstream <c>checkPacket</c>.
    /// </summary>
    public bool CheckHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2)
        {
            return false;
        }

        return ((header[0] ^ _iv[2]) & 0xFF) == ((_mapleVersion >> 8) & 0xFF)
            && ((header[1] ^ _iv[3]) & 0xFF) == (_mapleVersion & 0xFF);
    }

    /// <summary>Seeds the keystream buffer from the current IV (multiplyBytes(iv, 4, 4)).</summary>
    private void SeedKeystream()
    {
        // 16-byte seed = the 4-byte IV repeated four times.
        for (int i = 0; i < 16; i++)
        {
            _keystream[i] = _iv[i & 3];
        }
    }

    /// <summary>Advances the keystream: keystream = AES-ECB-encrypt(keystream).</summary>
    private void NextKeystreamBlock()
    {
        _aes.EncryptEcb(_keystream, _keystreamNext, PaddingMode.None);
        _keystreamNext.CopyTo(_keystream, 0);
    }

    private static ushort ByteSwap16(ushort value)
        => (ushort)(((value >> 8) & 0xFF) | ((value << 8) & 0xFF00));

    public void Dispose() => _aes.Dispose();
}
