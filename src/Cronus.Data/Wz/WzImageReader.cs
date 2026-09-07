namespace Cronus.Data.Wz;

/// <summary>
/// Parses one binary .img into a <see cref="WzNode"/> tree — the same walk as
/// <see cref="WzImageDumper"/>, but keeping everything (type codes, string encodings, canvas pixel
/// data, sound payloads) so <see cref="WzImageWriter"/> can serialize it back.
/// </summary>
public static class WzImageReader
{
    /// <summary>Parses the image, trying the archive's crypto first and then the other known IVs
    /// (pre-BB clients encrypt the images named in List.wz with a version IV).</summary>
    public static WzNode Read(WzArchive archive, WzImageEntry image)
        => ReadWithCrypto(archive, image).Root;

    /// <summary>Like <see cref="Read"/>, also returning the crypto that parsed the image (the one
    /// its canvas chunks are XOR'd with).</summary>
    public static (WzNode Root, WzCrypto Crypto) ReadWithCrypto(WzArchive archive, WzImageEntry image)
    {
        try
        {
            try
            {
                return (ReadWith(archive, image, crypto: null), archive.CurrentImageCrypto);
            }
            catch (InvalidDataException)
            {
                foreach (WzCrypto candidate in archive.ImageCryptoCandidates)
                {
                    try
                    {
                        return (ReadWith(archive, image, candidate), candidate);
                    }
                    catch (InvalidDataException)
                    {
                    }
                }

                throw;
            }
        }
        finally
        {
            archive.UseImageCrypto(null);
        }
    }

    private static WzNode ReadWith(WzArchive archive, WzImageEntry image, WzCrypto? crypto)
    {
        archive.UseImageCrypto(crypto);
        BinaryReader r = archive.Reader;
        r.BaseStream.Position = image.Offset;
        long end = image.Offset + image.Size;

        string kind = archive.ReadStringBlock(image.Offset);
        if (kind != "Property")
        {
            throw new InvalidDataException($"image root is '{kind}', not a Property container");
        }

        r.ReadUInt16(); // reserved
        var root = new WzNode(image.Name, WzNodeKind.Property);
        ReadPropertyList(archive, image.Offset, end, root.Children);
        return root;
    }

    private static void ReadPropertyList(WzArchive archive, long imageStart, long imageEnd, List<WzNode> into)
    {
        BinaryReader r = archive.Reader;
        int count = archive.ReadCompressedInt();
        if (count is < 0 or > 1_000_000)
        {
            throw new InvalidDataException($"implausible property count {count}");
        }

        for (int i = 0; i < count; i++)
        {
            string name = archive.ReadStringBlock(imageStart);
            bool nameUnicode = archive.LastStringWasUnicode;
            byte type = r.ReadByte();
            WzNode node;
            switch (type)
            {
                case 0:
                    node = new WzNode(name, WzNodeKind.Null);
                    break;

                case 2 or 11:
                    node = new WzNode(name, WzNodeKind.Short) { TypeCode = type, IntValue = r.ReadInt16() };
                    break;

                case 3 or 19:
                    node = new WzNode(name, WzNodeKind.Int) { TypeCode = type, IntValue = archive.ReadCompressedInt() };
                    break;

                case 20:
                    node = new WzNode(name, WzNodeKind.Long) { TypeCode = type, IntValue = archive.ReadCompressedLong() };
                    break;

                case 4:
                {
                    float f = r.ReadByte() == 0x80 ? r.ReadSingle() : 0f;
                    node = new WzNode(name, WzNodeKind.Float) { TypeCode = type, FloatValue = f };
                    break;
                }

                case 5:
                    node = new WzNode(name, WzNodeKind.Double) { TypeCode = type, FloatValue = r.ReadDouble() };
                    break;

                case 8:
                {
                    string value = archive.ReadStringBlock(imageStart);
                    node = new WzNode(name, WzNodeKind.String)
                    {
                        TypeCode = type,
                        StringValue = value,
                        StringIsUnicode = archive.LastStringWasUnicode,
                    };
                    break;
                }

                case 9:
                {
                    uint size = r.ReadUInt32();
                    long blockEnd = r.BaseStream.Position + size;
                    if (blockEnd > imageEnd)
                    {
                        throw new InvalidDataException($"extended block '{name}' runs past the image");
                    }

                    node = ReadExtended(archive, imageStart, imageEnd, name, blockEnd);
                    if (r.BaseStream.Position != blockEnd)
                    {
                        throw new InvalidDataException(
                            $"extended block '{name}' ({node.Kind}) consumed {r.BaseStream.Position - (blockEnd - size)} of {size} bytes");
                    }

                    break;
                }

                default:
                    throw new InvalidDataException($"unknown property type {type} (name '{name}')");
            }

            node.NameIsUnicode = nameUnicode;
            into.Add(node);
        }
    }

    /// <summary>Reads an extended block body at the current position. <paramref name="blockEnd"/>
    /// is the size-prefixed end, or -1 inside a Convex2D list (whose entries carry no size).</summary>
    private static WzNode ReadExtended(WzArchive archive, long imageStart, long imageEnd, string name, long blockEnd)
    {
        BinaryReader r = archive.Reader;
        string kind = archive.ReadStringBlock(imageStart);
        switch (kind)
        {
            case "Property":
            {
                r.ReadUInt16(); // reserved
                var node = new WzNode(name, WzNodeKind.Property);
                ReadPropertyList(archive, imageStart, imageEnd, node.Children);
                return node;
            }

            case "Canvas":
            {
                r.ReadByte();                       // reserved
                bool hasChildren = r.ReadByte() == 1;
                var node = new WzNode(name, WzNodeKind.Canvas);
                if (hasChildren)
                {
                    r.ReadUInt16();                 // reserved
                    ReadPropertyList(archive, imageStart, imageEnd, node.Children);
                }

                node.Width = archive.ReadCompressedInt();
                node.Height = archive.ReadCompressedInt();
                node.Format = archive.ReadCompressedInt();
                node.Format2 = r.ReadByte();
                r.ReadUInt32();                     // reserved
                int length = r.ReadInt32() - 1;     // the count includes a leading flag byte
                r.ReadByte();
                if (length < 0 || (blockEnd >= 0 && r.BaseStream.Position + length > blockEnd))
                {
                    throw new InvalidDataException($"canvas '{name}' data length {length} does not fit its block");
                }

                node.CanvasData = r.ReadBytes(length);
                return node;
            }

            case "Shape2D#Vector2D":
                return new WzNode(name, WzNodeKind.Vector)
                {
                    X = archive.ReadCompressedInt(),
                    Y = archive.ReadCompressedInt(),
                };

            case "Shape2D#Convex2D":
            {
                var node = new WzNode(name, WzNodeKind.Convex);
                int count = archive.ReadCompressedInt();
                if (count is < 0 or > 100_000)
                {
                    throw new InvalidDataException($"implausible convex point count {count}");
                }

                for (int i = 0; i < count; i++)
                {
                    node.Children.Add(ReadExtended(archive, imageStart, imageEnd, name, blockEnd: -1));
                }

                return node;
            }

            case "UOL":
            {
                r.ReadByte();                       // reserved
                string target = archive.ReadStringBlock(imageStart);
                return new WzNode(name, WzNodeKind.Uol)
                {
                    StringValue = target,
                    StringIsUnicode = archive.LastStringWasUnicode,
                };
            }

            case "Sound_DX8":
            {
                if (blockEnd < 0)
                {
                    throw new InvalidDataException("a sound cannot sit inside a convex list");
                }

                var node = new WzNode(name, WzNodeKind.Sound);
                node.RawPayload = r.ReadBytes((int)(blockEnd - r.BaseStream.Position));
                return node;
            }

            default:
                throw new InvalidDataException($"unknown extended property '{kind}' (name '{name}')");
        }
    }
}
