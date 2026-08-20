using System.Globalization;
using System.Xml;

namespace Cronus.Data;

/// <summary>
/// One node of a decoded WZ tree, mirroring the XML form used by wz_xml (ports the read side
/// of <c>odin.provider.WzXML.XMLDomMapleData</c> / <c>WzDataTool</c>). Each element carries a
/// <c>name</c> attribute; leaf elements (int/short/float/double/string/vector/canvas) carry a
/// typed value; <c>imgdir</c> elements carry children.
/// </summary>
public sealed class WzData
{
    private readonly Dictionary<string, WzData> _children = new(StringComparer.Ordinal);

    public WzData(string name) => Name = name;

    public string Name { get; }

    /// <summary>Raw string value of a leaf node, or null for a directory.</summary>
    public string? Value { get; init; }

    /// <summary>Second component of a <c>vector</c> node (y); the first is <see cref="Value"/> (x).</summary>
    public string? Value2 { get; init; }

    public IReadOnlyDictionary<string, WzData> Children => _children;

    internal void Add(WzData child) => _children[child.Name] = child;

    /// <summary>Direct child by name, or null.</summary>
    public WzData? Child(string name) => _children.TryGetValue(name, out WzData? c) ? c : null;

    /// <summary>Resolves a slash-separated path (e.g. "info/returnMap"), or null if any segment is missing.</summary>
    public WzData? Resolve(string path)
    {
        WzData current = this;
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            WzData? next = current.Child(segment);
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>This node's own value parsed as an int.</summary>
    public int AsInt(int fallback = 0)
        => int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    /// <summary>This node's own value as a string.</summary>
    public string AsString(string fallback = "") => Value ?? fallback;

    /// <summary>Int value at <paramref name="path"/> below this node, or <paramref name="fallback"/>.</summary>
    public int GetInt(string path, int fallback = 0) => Resolve(path)?.AsInt(fallback) ?? fallback;

    /// <summary>String value at <paramref name="path"/> below this node, or <paramref name="fallback"/>.</summary>
    public string GetString(string path, string fallback = "") => Resolve(path)?.AsString(fallback) ?? fallback;

    /// <summary>Parses a wz_xml document (a stream or file) into a <see cref="WzData"/> root.</summary>
    public static WzData Parse(Stream stream)
    {
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
        using XmlReader reader = XmlReader.Create(stream, settings);
        var doc = new XmlDocument();
        doc.Load(reader);

        XmlElement? root = doc.DocumentElement
            ?? throw new InvalidDataException("WZ XML document has no root element.");
        return FromElement(root);
    }

    public static WzData ParseFile(string path)
    {
        using FileStream fs = File.OpenRead(path);
        return Parse(fs);
    }

    private static WzData FromElement(XmlElement element)
    {
        string name = element.GetAttribute("name");
        var node = new WzData(name)
        {
            Value = ValueAttribute(element),
            Value2 = element.HasAttribute("y") ? element.GetAttribute("y") : null,
        };

        foreach (XmlNode childNode in element.ChildNodes)
        {
            if (childNode is XmlElement childElement)
            {
                node.Add(FromElement(childElement));
            }
        }

        return node;
    }

    // wz leaves keep their value in an attribute whose name depends on the element tag; for
    // "vector" the x-component is in "x". The tag itself is the type discriminator.
    private static string? ValueAttribute(XmlElement element) => element.LocalName switch
    {
        "int" or "short" or "float" or "double" or "string" => element.GetAttribute("value"),
        "vector" => element.GetAttribute("x"),
        "canvas" => element.HasAttribute("value") ? element.GetAttribute("value") : null,
        _ => null,
    };
}
