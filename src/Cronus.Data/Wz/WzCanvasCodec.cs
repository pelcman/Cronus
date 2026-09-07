using System.IO.Compression;

namespace Cronus.Data.Wz;

/// <summary>
/// Canvas pixel data: undoing the key-XOR'd chunking some archives apply, inflating the zlib
/// stream, decoding the pixel formats the pre-BB client uses, and encoding a PNG for eyeballing.
/// Ports the read side of MapleLib's <c>WzPngProperty</c>.
/// </summary>
public static class WzCanvasCodec
{
    /// <summary>True when the bytes start like a zlib stream (the client's own test for "not chunked").</summary>
    public static bool IsPlainZlib(ReadOnlySpan<byte> data)
        => data.Length >= 2 && data[0] == 0x78 && (data[1] == 0x9C || data[1] == 0xDA || data[1] == 0x01 || data[1] == 0x5E);

    /// <summary>
    /// The canvas data as one plain zlib stream: unchanged when it already is one, otherwise the
    /// key-XOR'd chunks ([int32 length][bytes ^ key]…) undone with <paramref name="crypto"/>.
    /// Plain zlib is what every client accepts, so a graft stores this form.
    /// </summary>
    public static byte[] ToPlainZlib(WzNode canvas, WzCrypto crypto)
        => IsPlainZlib(canvas.CanvasData) ? canvas.CanvasData : Dechunk(canvas.CanvasData, crypto);

    private static byte[] Dechunk(byte[] data, WzCrypto crypto)
    {
        var output = new MemoryStream(data.Length);
        int pos = 0;
        while (pos + 4 <= data.Length)
        {
            int length = BitConverter.ToInt32(data, pos);
            pos += 4;
            if (length < 0 || pos + length > data.Length)
            {
                throw new InvalidDataException($"canvas chunk of {length} bytes at {pos - 4} does not fit");
            }

            for (int i = 0; i < length; i++)
            {
                output.WriteByte((byte)(data[pos + i] ^ crypto.KeyAt(i)));
            }

            pos += length;
        }

        return output.ToArray();
    }

    /// <summary>The inflated pixel bytes in the canvas's own format.</summary>
    public static byte[] Inflate(WzNode canvas, WzCrypto crypto)
    {
        byte[] zlib = ToPlainZlib(canvas, crypto);
        using var input = new MemoryStream(zlib);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Decodes the canvas to 32-bit BGRA (width × height × 4).</summary>
    public static byte[] DecodeBgra(WzNode canvas, WzCrypto crypto)
    {
        int width = canvas.Width;
        int height = canvas.Height;
        byte[] raw = Inflate(canvas, crypto);
        var pixels = new byte[width * height * 4];
        int format = canvas.Format + canvas.Format2;
        switch (format)
        {
            case 1: // BGRA4444
            {
                for (int i = 0; i < width * height && i * 2 + 1 < raw.Length; i++)
                {
                    ushort v = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                    pixels[i * 4 + 0] = (byte)((v & 0xF) * 17);
                    pixels[i * 4 + 1] = (byte)(((v >> 4) & 0xF) * 17);
                    pixels[i * 4 + 2] = (byte)(((v >> 8) & 0xF) * 17);
                    pixels[i * 4 + 3] = (byte)(((v >> 12) & 0xF) * 17);
                }

                break;
            }

            case 2: // BGRA8888
                Array.Copy(raw, pixels, Math.Min(raw.Length, pixels.Length));
                break;

            case 513: // RGB565
            {
                for (int i = 0; i < width * height && i * 2 + 1 < raw.Length; i++)
                {
                    ushort v = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                    WriteRgb565(pixels, i * 4, v);
                }

                break;
            }

            case 517: // RGB565, one value per 16×16 block
            {
                int blocksX = Math.Max(1, width / 16);
                for (int by = 0; by < Math.Max(1, height / 16); by++)
                {
                    for (int bx = 0; bx < blocksX; bx++)
                    {
                        int src = (by * blocksX + bx) * 2;
                        if (src + 1 >= raw.Length)
                        {
                            break;
                        }

                        ushort v = (ushort)(raw[src] | (raw[src + 1] << 8));
                        for (int y = by * 16; y < Math.Min(height, by * 16 + 16); y++)
                        {
                            for (int x = bx * 16; x < Math.Min(width, bx * 16 + 16); x++)
                            {
                                WriteRgb565(pixels, (y * width + x) * 4, v);
                            }
                        }
                    }
                }

                break;
            }

            case 1026: // DXT3
                DecodeDxt(raw, pixels, width, height, dxt5: false);
                break;

            case 2050: // DXT5
                DecodeDxt(raw, pixels, width, height, dxt5: true);
                break;

            default:
                throw new NotSupportedException($"canvas pixel format {format} is not supported");
        }

        return pixels;
    }

    private static void WriteRgb565(byte[] pixels, int at, ushort v)
    {
        pixels[at + 0] = (byte)((v & 0x1F) * 255 / 31);
        pixels[at + 1] = (byte)(((v >> 5) & 0x3F) * 255 / 63);
        pixels[at + 2] = (byte)(((v >> 11) & 0x1F) * 255 / 31);
        pixels[at + 3] = 255;
    }

    private static void DecodeDxt(byte[] raw, byte[] pixels, int width, int height, bool dxt5)
    {
        int pos = 0;
        for (int by = 0; by < height; by += 4)
        {
            for (int bx = 0; bx < width; bx += 4)
            {
                if (pos + 16 > raw.Length)
                {
                    return;
                }

                var alpha = new byte[16];
                if (dxt5)
                {
                    byte a0 = raw[pos], a1 = raw[pos + 1];
                    var table = new byte[8];
                    table[0] = a0;
                    table[1] = a1;
                    if (a0 > a1)
                    {
                        for (int i = 1; i < 7; i++)
                        {
                            table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < 5; i++)
                        {
                            table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                        }

                        table[6] = 0;
                        table[7] = 255;
                    }

                    ulong bits = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        bits |= (ulong)raw[pos + 2 + i] << (8 * i);
                    }

                    for (int i = 0; i < 16; i++)
                    {
                        alpha[i] = table[(bits >> (3 * i)) & 7];
                    }
                }
                else
                {
                    for (int i = 0; i < 16; i++)
                    {
                        int nibble = (raw[pos + i / 2] >> ((i & 1) * 4)) & 0xF;
                        alpha[i] = (byte)(nibble * 17);
                    }
                }

                int c = pos + 8;
                ushort c0 = (ushort)(raw[c] | (raw[c + 1] << 8));
                ushort c1 = (ushort)(raw[c + 2] | (raw[c + 3] << 8));
                var colors = new byte[4][];
                colors[0] = Rgb565(c0);
                colors[1] = Rgb565(c1);
                colors[2] = new byte[]
                {
                    (byte)((2 * colors[0][0] + colors[1][0]) / 3),
                    (byte)((2 * colors[0][1] + colors[1][1]) / 3),
                    (byte)((2 * colors[0][2] + colors[1][2]) / 3),
                };
                colors[3] = new byte[]
                {
                    (byte)((colors[0][0] + 2 * colors[1][0]) / 3),
                    (byte)((colors[0][1] + 2 * colors[1][1]) / 3),
                    (byte)((colors[0][2] + 2 * colors[1][2]) / 3),
                };
                uint indices = (uint)(raw[c + 4] | (raw[c + 5] << 8) | (raw[c + 6] << 16) | (raw[c + 7] << 24));
                for (int i = 0; i < 16; i++)
                {
                    int x = bx + (i & 3), y = by + (i >> 2);
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    byte[] color = colors[(indices >> (2 * i)) & 3];
                    int at = (y * width + x) * 4;
                    pixels[at + 0] = color[0];
                    pixels[at + 1] = color[1];
                    pixels[at + 2] = color[2];
                    pixels[at + 3] = alpha[i];
                }

                pos += 16;
            }
        }

        static byte[] Rgb565(ushort v) => new[]
        {
            (byte)((v & 0x1F) * 255 / 31),
            (byte)(((v >> 5) & 0x3F) * 255 / 63),
            (byte)(((v >> 11) & 0x1F) * 255 / 31),
        };
    }

    /// <summary>Encodes BGRA pixels as an RGBA PNG (8-bit, filter 0, one IDAT).</summary>
    public static byte[] EncodePng(int width, int height, ReadOnlySpan<byte> bgra)
    {
        var rows = new byte[height * (width * 4 + 1)];
        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 4 + 1);
            rows[row] = 0;
            for (int x = 0; x < width; x++)
            {
                int src = (y * width + x) * 4;
                int dst = row + 1 + x * 4;
                rows[dst + 0] = bgra[src + 2];
                rows[dst + 1] = bgra[src + 1];
                rows[dst + 2] = bgra[src + 0];
                rows[dst + 3] = bgra[src + 3];
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                z.Write(rows);
            }

            compressed = ms.ToArray();
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, data.Length);
        s.Write(len);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        s.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int at, int value)
    {
        buffer[at] = (byte)(value >> 24);
        buffer[at + 1] = (byte)(value >> 16);
        buffer[at + 2] = (byte)(value >> 8);
        buffer[at + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint crc)
    {
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }
}
