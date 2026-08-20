using Cronus.Network.Crypto;
using Xunit;

namespace Cronus.Network.Tests;

public class AesOfbCipherTests
{
    private const int Version = 186;

    private static byte[] SampleIv => new byte[] { 82, 48, 120, 0x5A };

    [Fact]
    public void Crypt_IsSymmetric_WithMatchingIv()
    {
        byte[] plaintext = System.Text.Encoding.ASCII.GetBytes("The quick brown fox jumps over 13 lazy dogs.");
        byte[] expected = (byte[])plaintext.Clone();

        // Two ciphers seeded identically produce identical keystreams; XOR twice recovers.
        using var encrypt = new AesOfbCipher(SampleIv, Version, outbound: true);
        using var decrypt = new AesOfbCipher(SampleIv, Version, outbound: true);

        encrypt.Crypt(plaintext);
        Assert.NotEqual(expected, plaintext); // actually transformed

        decrypt.Crypt(plaintext);
        Assert.Equal(expected, plaintext);
    }

    [Fact]
    public void Crypt_RoundTrips_AcrossManyPacketsWithIvAdvance()
    {
        using var send = new AesOfbCipher(SampleIv, Version, outbound: true);
        using var recv = new AesOfbCipher(SampleIv, Version, outbound: true);

        for (int i = 0; i < 50; i++)
        {
            byte[] message = new byte[1 + (i * 7)];
            for (int j = 0; j < message.Length; j++)
            {
                message[j] = (byte)(j + i);
            }

            byte[] original = (byte[])message.Clone();

            send.Crypt(message);
            recv.Crypt(message);
            Assert.Equal(original, message);

            // IVs must stay in lockstep so the next packet also round-trips.
            send.AdvanceIv();
            recv.AdvanceIv();
            Assert.Equal(send.Iv, recv.Iv);
        }
    }

    [Fact]
    public void Crypt_HandlesPayloadsLargerThanOneBlock()
    {
        // Larger than the 0x5B0 first-block boundary to exercise multi-block keystreaming.
        byte[] payload = new byte[0x5B0 + 0x5B4 + 123];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i * 31);
        }

        byte[] original = (byte[])payload.Clone();

        using var send = new AesOfbCipher(SampleIv, Version, outbound: true);
        using var recv = new AesOfbCipher(SampleIv, Version, outbound: true);
        send.Crypt(payload);
        recv.Crypt(payload);

        Assert.Equal(original, payload);
    }

    [Fact]
    public void WriteHeader_ThenReadLength_RecoversLength()
    {
        using var cipher = new AesOfbCipher(SampleIv, Version, outbound: true);
        Span<byte> header = stackalloc byte[4];

        foreach (int length in new[] { 2, 16, 300, 0x5B0, ushort.MaxValue })
        {
            cipher.WriteHeader(header, length);
            Assert.Equal(length, AesOfbCipher.ReadLength(header));
        }
    }

    [Fact]
    public void CheckHeader_AcceptsHeaderWrittenBySameCipher()
    {
        using var cipher = new AesOfbCipher(SampleIv, Version, outbound: true);
        Span<byte> header = stackalloc byte[4];
        cipher.WriteHeader(header, 123);

        Assert.True(cipher.CheckHeader(header));
    }

    [Fact]
    public void CheckHeader_RejectsHeaderForWrongIv()
    {
        using var writer = new AesOfbCipher(SampleIv, Version, outbound: true);
        using var other = new AesOfbCipher(new byte[] { 1, 2, 3, 4 }, Version, outbound: true);

        Span<byte> header = stackalloc byte[4];
        writer.WriteHeader(header, 123);

        Assert.False(other.CheckHeader(header));
    }
}
