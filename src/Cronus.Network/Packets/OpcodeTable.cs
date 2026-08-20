using System.Globalization;

namespace Cronus.Network.Packets;

/// <summary>
/// Maps opcode name identifiers to their per-version numeric values, loaded from a
/// <c>.properties</c> file (ports <c>tacos.property.Property_Packet</c>). Opcode names are
/// stable across versions; only the values change, so the values live as external data.
///
/// Supported line forms:
/// <list type="bullet">
///   <item><c>NAME = @001D</c> — hex value.</item>
///   <item><c>NAME = 29</c> — decimal value.</item>
///   <item><c>NAME = BASE + 3</c> / <c>NAME = BASE - 1</c> — relative to another opcode.</item>
///   <item><c>NAME</c> (no <c>=</c>) — section marker / undefined (value -1).</item>
/// </list>
/// Lines beginning with <c>#</c> and blank lines are ignored.
/// </summary>
public sealed class OpcodeTable
{
    /// <summary>Value used for undefined opcodes (upstream sentinel <c>@FFFF</c> / -1).</summary>
    public const int Undefined = -1;

    private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);

    /// <summary>Resolved numeric value for <paramref name="name"/>, or -1 if undefined.</summary>
    public int this[string name] => _values.TryGetValue(name, out int value) ? value : Undefined;

    /// <summary>Resolved numeric value for <paramref name="name"/>, or -1 if undefined.</summary>
    public int Get(string name) => this[name];

    /// <summary>True if <paramref name="name"/> resolved to a real (non -1) value.</summary>
    public bool IsDefined(string name) => this[name] != Undefined;

    /// <summary>All resolved (name, value) pairs with a defined value.</summary>
    public IEnumerable<KeyValuePair<string, int>> Entries
        => _values.Where(kv => kv.Value != Undefined);

    /// <summary>Names that appeared in the file but could not be resolved to a value.</summary>
    public IReadOnlyList<string> UnresolvedNames { get; private set; } = Array.Empty<string>();

    public static OpcodeTable LoadFile(string path)
        => Load(File.ReadLines(path));

    public static OpcodeTable Load(IEnumerable<string> lines)
    {
        var table = new OpcodeTable();
        var pending = new List<(string Name, string Expr)>();

        foreach (string raw in lines)
        {
            string line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                // Section marker / undefined opcode.
                table._values[line] = Undefined;
                continue;
            }

            string name = line[..eq].Trim();
            string expr = line[(eq + 1)..].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (TryParseLiteral(expr, out int literal))
            {
                table._values[name] = literal;
            }
            else
            {
                // Relative form (BASE +/- offset): resolve after all literals are loaded.
                table._values[name] = Undefined;
                pending.Add((name, expr));
            }
        }

        ResolveRelatives(table, pending);
        return table;
    }

    private static void ResolveRelatives(OpcodeTable table, List<(string Name, string Expr)> pending)
    {
        // Iterate to a fixed point: a relative entry may reference another relative entry.
        bool progressed = true;
        while (progressed && pending.Count > 0)
        {
            progressed = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                (string name, string expr) = pending[i];
                if (TryResolveRelative(table, expr, out int value))
                {
                    table._values[name] = value;
                    pending.RemoveAt(i);
                    progressed = true;
                }
            }
        }

        table.UnresolvedNames = pending.Count == 0
            ? Array.Empty<string>()
            : pending.Select(p => p.Name).ToArray();
    }

    private static bool TryResolveRelative(OpcodeTable table, string expr, out int value)
    {
        value = Undefined;

        int sign = 0;
        int opIndex = -1;
        for (int i = 0; i < expr.Length; i++)
        {
            if (expr[i] == '+')
            {
                sign = 1;
                opIndex = i;
                break;
            }

            if (expr[i] == '-')
            {
                sign = -1;
                opIndex = i;
                break;
            }
        }

        if (opIndex < 0)
        {
            return false;
        }

        string baseName = expr[..opIndex].Trim();
        string offsetText = expr[(opIndex + 1)..].Trim();
        if (!int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset))
        {
            return false;
        }

        int baseValue = table[baseName];
        if (baseValue == Undefined)
        {
            return false;
        }

        value = baseValue + (sign * offset);
        return true;
    }

    private static bool TryParseLiteral(string expr, out int value)
    {
        value = Undefined;
        if (expr.Length == 0)
        {
            return false;
        }

        if (expr[0] == '@')
        {
            return int.TryParse(expr.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string StripComment(string line)
    {
        int hash = line.IndexOf('#');
        return hash < 0 ? line : line[..hash];
    }
}
