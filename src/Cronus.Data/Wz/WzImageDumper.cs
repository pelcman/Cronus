using System.Globalization;
using System.Text;

namespace Cronus.Data.Wz;

/// <summary>
/// Parses one binary .img property tree and renders it as a wz_xml document — the exact
/// single-line XML shape the rest of Cronus.Data consumes (and the existing DevTools dump uses):
/// <c>imgdir</c>/<c>int</c>/<c>float</c>/<c>string</c>/<c>vector</c>/<c>canvas</c>/<c>uol</c>
/// elements with name-first attributes. Canvas pixel data and sounds are structure only — the
/// server never renders, so width/height and the child properties are all that is kept.
/// </summary>
public static class WzImageDumper
{
    public static string DumpXml(WzArchive archive, WzImageEntry image)
    {
        // Most images share the archive's crypto, but pre-BB clients AES-encrypt the images
        // named in List.wz — detected here by the root container failing to say "Property",
        // in which case the other known IVs are tried before giving up.
        try
        {
            return DumpXmlWith(archive, image, crypto: null);
        }
        catch (InvalidDataException)
        {
            foreach (WzCrypto candidate in archive.ImageCryptoCandidates)
            {
                try
                {
                    return DumpXmlWith(archive, image, candidate);
                }
                catch (InvalidDataException)
                {
                }
            }

            throw;
        }
        finally
        {
            archive.UseImageCrypto(null);
        }
    }

    private static string DumpXmlWith(WzArchive archive, WzImageEntry image, WzCrypto? crypto)
    {
        archive.UseImageCrypto(crypto);
        var sb = new StringBuilder(4096);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        BinaryReader r = archive.Reader;
        r.BaseStream.Position = image.Offset;

        sb.Append("<imgdir name=\"").Append(Escape(image.Name)).Append('"').Append('>');
        ReadPropertyContainerBody(archive, image.Offset, sb);
        sb.Append("</imgdir>");
        return sb.ToString();
    }

    /// <summary>Reads a "Property" container at the current position: its type string, the
    /// 2 reserved bytes, and the child list.</summary>
    private static void ReadPropertyContainerBody(WzArchive archive, long imageStart, StringBuilder sb)
    {
        string kind = archive.ReadStringBlock(imageStart);
        if (kind != "Property")
        {
            throw new InvalidDataException($"image root is '{kind}', not a Property container");
        }

        archive.Reader.ReadUInt16(); // reserved
        ReadPropertyList(archive, imageStart, sb);
    }

    private static void ReadPropertyList(WzArchive archive, long imageStart, StringBuilder sb)
    {
        BinaryReader r = archive.Reader;
        int count = archive.ReadCompressedInt();
        for (int i = 0; i < count; i++)
        {
            string name = archive.ReadStringBlock(imageStart);
            byte type = r.ReadByte();
            switch (type)
            {
                case 0: // null property: no payload — kept as a marker element like the dump
                    sb.Append("<null name=\"").Append(Escape(name)).Append("\"/>");
                    break;

                case 2 or 11: // short
                    AppendLeaf(sb, "short", name, r.ReadInt16().ToString(CultureInfo.InvariantCulture));
                    break;

                case 3 or 19: // int
                    AppendLeaf(sb, "int", name, archive.ReadCompressedInt().ToString(CultureInfo.InvariantCulture));
                    break;

                case 20: // long (post-BB era, defensive)
                    AppendLeaf(sb, "int", name, archive.ReadCompressedLong().ToString(CultureInfo.InvariantCulture));
                    break;

                case 4: // float, present only when the marker byte says so
                {
                    float f = r.ReadByte() == 0x80 ? r.ReadSingle() : 0f;
                    AppendLeaf(sb, "float", name, FormatFloat(f));
                    break;
                }

                case 5: // double
                    AppendLeaf(sb, "double", name, FormatFloat(r.ReadDouble()));
                    break;

                case 8: // string
                    AppendLeaf(sb, "string", name, archive.ReadStringBlock(imageStart));
                    break;

                case 9: // extended block, length-prefixed so a skip miscount cannot derail the list
                {
                    uint size = r.ReadUInt32();
                    long end = r.BaseStream.Position + size;
                    ReadExtended(archive, imageStart, name, sb);
                    r.BaseStream.Position = end;
                    break;
                }

                default:
                    throw new InvalidDataException($"unknown property type {type} (name '{name}')");
            }
        }
    }

    private static void ReadExtended(WzArchive archive, long imageStart, string name, StringBuilder sb)
    {
        BinaryReader r = archive.Reader;
        string kind = archive.ReadStringBlock(imageStart);
        switch (kind)
        {
            case "Property":
                r.ReadUInt16(); // reserved
                sb.Append("<imgdir name=\"").Append(Escape(name)).Append("\">");
                ReadPropertyList(archive, imageStart, sb);
                sb.Append("</imgdir>");
                break;

            case "Canvas":
            {
                r.ReadByte();                       // reserved
                bool hasChildren = r.ReadByte() == 1;
                var children = new StringBuilder();
                if (hasChildren)
                {
                    r.ReadUInt16();                 // reserved
                    ReadPropertyList(archive, imageStart, children);
                }

                int width = archive.ReadCompressedInt();
                int height = archive.ReadCompressedInt();
                // pixel data (format, length, bytes) follows; the enclosing size-prefixed
                // block seek skips it, so nothing more to read here.
                sb.Append("<canvas name=\"").Append(Escape(name))
                    .Append("\" width=\"").Append(width)
                    .Append("\" height=\"").Append(height).Append('"');
                sb.Append('>').Append(children).Append("</canvas>"); // never self-closed, like the dump

                break;
            }

            case "Shape2D#Vector2D":
            {
                int x = archive.ReadCompressedInt();
                int y = archive.ReadCompressedInt();
                sb.Append("<vector name=\"").Append(Escape(name))
                    .Append("\" x=\"").Append(x)
                    .Append("\" y=\"").Append(y).Append("\"/>");
                break;
            }

            case "Shape2D#Convex2D":
            {
                // A list of unnamed vectors, indexed; the dump renders these as <extended>.
                sb.Append("<extended name=\"").Append(Escape(name)).Append("\">");
                int count = archive.ReadCompressedInt();
                for (int i = 0; i < count; i++)
                {
                    ReadExtended(archive, imageStart, name, sb); // children inherit the parent name
                }

                sb.Append("</extended>");
                break;
            }

            case "UOL":
            {
                r.ReadByte();                       // reserved
                string target = archive.ReadStringBlock(imageStart);
                AppendLeaf(sb, "uol", name, target);
                break;
            }

            case "Sound_DX8":
                // Audio payload — nothing the server consumes; the block seek skips it.
                break;

            default:
                throw new InvalidDataException($"unknown extended property '{kind}' (name '{name}')");
        }
    }

    private static void AppendLeaf(StringBuilder sb, string tag, string name, string value)
        => sb.Append('<').Append(tag)
            .Append(" name=\"").Append(Escape(name))
            .Append("\" value=\"").Append(Escape(value))
            .Append("\"/>");

    /// <summary>Single-precision values format with 7 significant digits plus a forced ".0" on
    /// integral values — the .NET-Framework-era style the reference dump used, so 1.6000001f
    /// prints as "1.6" and 1f as "1.0" (shortest-roundtrip would say "1.6000001" / "1").</summary>
    private static string FormatFloat(float value)
    {
        string s = value.ToString("G7", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') || s.Contains("Inf") || s.Contains("NaN") ? s : s + ".0";
    }

    /// <summary>Doubles print with 15 significant digits and no forced ".0" — again the exact
    /// convention of the dump this replaces ("0", "0.3").</summary>
    private static string FormatFloat(double value)
        => value.ToString("G15", CultureInfo.InvariantCulture);

    private static string Escape(string s)
    {
        if (s.AsSpan().IndexOfAny('&', '<', '>') < 0 && s.AsSpan().IndexOfAny('"', '\'') < 0)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}
