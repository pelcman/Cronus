namespace Cronus.Network.Crypto;

/// <summary>
/// The "shanda" custom encryption layer (ports <c>tacos.network.MapleCustomEncryption</c>).
/// Applied by some regions on top of AES-OFB. JMS does NOT use it (see
/// <see cref="Cronus.Common.ServerConfig.CustomEncryption"/> = false), but it is ported for
/// completeness and so other-region experiments stay possible.
///
/// Both transforms run six passes, alternating direction and mixing each byte with a running
/// "remember" value, the descending length counter, and fixed rotates. Encrypt and decrypt
/// are inverse operations, not symmetric.
/// </summary>
public static class ShandaCipher
{
    private static byte RollLeft(byte value, int count)
    {
        int tmp = (value & 0xFF) << (count % 8);
        return (byte)((tmp & 0xFF) | (tmp >> 8));
    }

    private static byte RollRight(byte value, int count)
    {
        int tmp = ((value & 0xFF) << 8) >> (count % 8);
        return (byte)((tmp & 0xFF) | (tmp >> 8));
    }

    /// <summary>Encrypts <paramref name="data"/> in place.</summary>
    public static void Encrypt(Span<byte> data)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            byte remember = 0;
            byte length = (byte)(data.Length & 0xFF);

            if (pass % 2 == 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    byte cur = RollLeft(data[i], 3);
                    cur = (byte)(cur + length);
                    cur ^= remember;
                    remember = cur;
                    cur = RollRight(cur, length & 0xFF);
                    cur = (byte)(~cur & 0xFF);
                    cur = (byte)(cur + 0x48);
                    length--;
                    data[i] = cur;
                }
            }
            else
            {
                for (int i = data.Length - 1; i >= 0; i--)
                {
                    byte cur = RollLeft(data[i], 4);
                    cur = (byte)(cur + length);
                    cur ^= remember;
                    remember = cur;
                    cur ^= 0x13;
                    cur = RollRight(cur, 3);
                    length--;
                    data[i] = cur;
                }
            }
        }
    }

    /// <summary>Decrypts <paramref name="data"/> in place.</summary>
    public static void Decrypt(Span<byte> data)
    {
        for (int pass = 1; pass <= 6; pass++)
        {
            byte remember = 0;
            byte length = (byte)(data.Length & 0xFF);

            if (pass % 2 == 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    byte cur = (byte)(data[i] - 0x48);
                    cur = (byte)(~cur & 0xFF);
                    cur = RollLeft(cur, length & 0xFF);
                    byte next = cur;
                    cur ^= remember;
                    remember = next;
                    cur = (byte)(cur - length);
                    cur = RollRight(cur, 3);
                    data[i] = cur;
                    length--;
                }
            }
            else
            {
                for (int i = data.Length - 1; i >= 0; i--)
                {
                    byte cur = RollLeft(data[i], 3);
                    cur ^= 0x13;
                    byte next = cur;
                    cur ^= remember;
                    remember = next;
                    cur = (byte)(cur - length);
                    cur = RollRight(cur, 4);
                    data[i] = cur;
                    length--;
                }
            }
        }
    }
}
