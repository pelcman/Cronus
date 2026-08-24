namespace Cronus.Data;

/// <summary>Validates avatar style ids (hair / face / skin) against the game data.</summary>
public interface IStyleProvider
{
    bool IsValidHair(int hairId);

    bool IsValidFace(int faceId);

    bool IsValidSkin(int skinColor);

    /// <summary>Every valid hair id, ascending (30xxx male / 31xxx female, color digit included).</summary>
    IReadOnlyList<int> AllHairs();

    /// <summary>Every valid face id, ascending (20xxx male / 21xxx female, color hundreds digit included).</summary>
    IReadOnlyList<int> AllFaces();

    /// <summary>Every valid skin color, ascending (0-based).</summary>
    IReadOnlyList<int> AllSkins();
}

/// <summary>
/// Enumerates the valid style ids from the wz Character tree (the C# equivalent of
/// <c>WzDataStorage.HAIR/FACE/SKIN.check</c>). A style is valid when its imgdir file exists —
/// <c>Character/Hair/000{hair}.img.xml</c>, <c>Character/Face/000{face}.img.xml</c>, and the
/// body file <c>Character/0000{2000+skin}.img.xml</c> — so a script can never set a look the
/// client has no data for (which would crash it).
/// </summary>
public sealed class WzStyleProvider : IStyleProvider
{
    private readonly Lazy<(HashSet<int> Hairs, HashSet<int> Faces, HashSet<int> Bodies)> _sets;

    public WzStyleProvider(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzStyleProvider(IWzStore store)
    {
        _sets = new(() => Load(store));
    }

    public bool IsValidHair(int hairId) => _sets.Value.Hairs.Contains(hairId);

    public bool IsValidFace(int faceId) => _sets.Value.Faces.Contains(faceId);

    public bool IsValidSkin(int skinColor) => _sets.Value.Bodies.Contains(2000 + skinColor);

    public IReadOnlyList<int> AllHairs() => _sets.Value.Hairs.Order().ToList();

    public IReadOnlyList<int> AllFaces() => _sets.Value.Faces.Order().ToList();

    public IReadOnlyList<int> AllSkins()
        => _sets.Value.Bodies.Where(b => b is >= 2000 and < 2100).Select(b => b - 2000).Order().ToList();

    private static (HashSet<int>, HashSet<int>, HashSet<int>) Load(IWzStore store)
        => (IdsIn(store, "Character/Hair"), IdsIn(store, "Character/Face"), IdsIn(store, "Character"));

    /// <summary>Numeric image ids that are DIRECT children of <paramref name="prefix"/> (skins
    /// live at the Character root, so nested folders must not leak in).</summary>
    private static HashSet<int> IdsIn(IWzStore store, string prefix)
    {
        var ids = new HashSet<int>();
        foreach (string path in store.EnumeratePaths(prefix))
        {
            string rest = path[(prefix.Length + 1)..];
            int dot = rest.IndexOf('.');
            if (!rest.Contains('/') && dot > 0 && int.TryParse(rest[..dot], out int id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
