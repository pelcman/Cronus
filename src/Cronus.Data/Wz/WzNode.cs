namespace Cronus.Data.Wz;

/// <summary>The property kinds a .img holds (the binary type codes and extended-block names).</summary>
public enum WzNodeKind
{
    Null,
    Short,
    Int,
    Long,
    Float,
    Double,
    String,
    Property,
    Canvas,
    Vector,
    Convex,
    Uol,
    Sound,
}

/// <summary>
/// One node of a parsed .img property tree, kept close to the binary form so a rewrite
/// reproduces the original bytes: leaf type codes, the string encodings, and the canvas / sound
/// payloads exactly as stored (still encrypted or chunked when they were).
/// </summary>
public sealed class WzNode
{
    public WzNode(string name, WzNodeKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; set; }

    public WzNodeKind Kind { get; }

    /// <summary>Whether the name was stored as UTF-16 (the writer keeps the form).</summary>
    public bool NameIsUnicode { get; set; }

    /// <summary>The raw leaf type code (2/11 short, 3/19 int) so the rewrite keeps it.</summary>
    public byte TypeCode { get; set; }

    /// <summary>Short / Int / Long value.</summary>
    public long IntValue { get; set; }

    /// <summary>Float / Double value.</summary>
    public double FloatValue { get; set; }

    /// <summary>String value, or the UOL target.</summary>
    public string StringValue { get; set; } = string.Empty;

    /// <summary>Whether the string value was stored as UTF-16.</summary>
    public bool StringIsUnicode { get; set; }

    /// <summary>Children: a Property's members, a Canvas's sub-properties, a Convex's vectors.</summary>
    public List<WzNode> Children { get; } = new();

    /// <summary>Vector x.</summary>
    public int X { get; set; }

    /// <summary>Vector y.</summary>
    public int Y { get; set; }

    /// <summary>Canvas width.</summary>
    public int Width { get; set; }

    /// <summary>Canvas height.</summary>
    public int Height { get; set; }

    /// <summary>Canvas pixel format (1 = BGRA4444, 2 = BGRA8888, 513 = RGB565, 517 = RGB565 ×16, 1026/2050 = DXT3/5).</summary>
    public int Format { get; set; }

    /// <summary>Canvas format2 byte (adds to the format in some readers; kept verbatim).</summary>
    public byte Format2 { get; set; }

    /// <summary>Canvas pixel data as stored: a zlib stream, or key-XOR'd chunks of one.</summary>
    public byte[] CanvasData { get; set; } = Array.Empty<byte>();

    /// <summary>A Sound_DX8 block's bytes after its kind string, verbatim (header + audio).</summary>
    public byte[] RawPayload { get; set; } = Array.Empty<byte>();

    public WzNode? Child(string name)
        => Children.FirstOrDefault(c => c.Name == name);

    /// <summary>Navigates a slash path ("ship/ossyria/97"); null when a segment is missing.</summary>
    public WzNode? Find(string path)
    {
        WzNode current = this;
        foreach (string segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            WzNode? next = current.Child(segment);
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>Replaces the child named like <paramref name="node"/> (keeping its slot) or appends it.</summary>
    public void Put(WzNode node)
    {
        int index = Children.FindIndex(c => c.Name == node.Name);
        if (index >= 0)
        {
            Children[index] = node;
        }
        else
        {
            Children.Add(node);
        }
    }

    /// <summary>Every node in the subtree (this one first), depth-first.</summary>
    public IEnumerable<WzNode> Descendants()
    {
        yield return this;
        foreach (WzNode child in Children)
        {
            foreach (WzNode n in child.Descendants())
            {
                yield return n;
            }
        }
    }

    /// <summary>A one-line description for listings.</summary>
    public string Describe() => Kind switch
    {
        WzNodeKind.Short or WzNodeKind.Int or WzNodeKind.Long => $"{Kind.ToString().ToLowerInvariant()} {IntValue}",
        WzNodeKind.Float or WzNodeKind.Double => $"{Kind.ToString().ToLowerInvariant()} {FloatValue}",
        WzNodeKind.String => $"string \"{StringValue}\"",
        WzNodeKind.Uol => $"uol -> {StringValue}",
        WzNodeKind.Vector => $"vector ({X}, {Y})",
        WzNodeKind.Canvas => $"canvas {Width}x{Height} fmt {Format}+{Format2} ({CanvasData.Length} bytes){(Children.Count > 0 ? $" +{Children.Count} props" : "")}",
        WzNodeKind.Sound => $"sound ({RawPayload.Length} bytes)",
        WzNodeKind.Property => $"property ({Children.Count} children)",
        WzNodeKind.Convex => $"convex ({Children.Count} points)",
        _ => "null",
    };
}
