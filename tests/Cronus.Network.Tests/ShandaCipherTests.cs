using Cronus.Network.Crypto;
using Xunit;

namespace Cronus.Network.Tests;

public class ShandaCipherTests
{
    [Fact]
    public void EncryptThenDecryptRecoversOriginal()
    {
        byte[] original = System.Text.Encoding.ASCII.GetBytes("shanda round-trip payload 0123456789");
        byte[] data = (byte[])original.Clone();

        ShandaCipher.Encrypt(data);
        Assert.NotEqual(original, data);

        ShandaCipher.Decrypt(data);
        Assert.Equal(original, data);
    }

    [Fact]
    public void RoundTripsVariousLengths()
    {
        for (int length = 0; length <= 300; length += 17)
        {
            byte[] original = new byte[length];
            for (int i = 0; i < length; i++)
            {
                original[i] = (byte)(i * 5 + 1);
            }

            byte[] data = (byte[])original.Clone();
            ShandaCipher.Encrypt(data);
            ShandaCipher.Decrypt(data);

            Assert.Equal(original, data);
        }
    }
}
