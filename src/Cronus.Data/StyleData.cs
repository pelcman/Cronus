namespace Cronus.Data;

/// <summary>Validates avatar style ids (hair / face / skin) against the game data.</summary>
public interface IStyleProvider
{
    bool IsValidHair(int hairId);

    bool IsValidFace(int faceId);

    bool IsValidSkin(int skinColor);
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

    public WzStyleProvider(string wzRoot)
    {
        _sets = new(() => Load(wzRoot));
    }

    public bool IsValidHair(int hairId) => _sets.Value.Hairs.Contains(hairId);

    public bool IsValidFace(int faceId) => _sets.Value.Faces.Contains(faceId);

    public bool IsValidSkin(int skinColor) => _sets.Value.Bodies.Contains(2000 + skinColor);

    private static (HashSet<int>, HashSet<int>, HashSet<int>) Load(string wzRoot)
    {
        string root = Path.Combine(wzRoot, "Character");
        return (
            IdsIn(Path.Combine(root, "Hair")),
            IdsIn(Path.Combine(root, "Face")),
            IdsIn(root));
    }

    private static HashSet<int> IdsIn(string dir)
    {
        var ids = new HashSet<int>();
        if (!Directory.Exists(dir))
        {
            return ids;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.img.xml"))
        {
            string name = Path.GetFileName(file);
            int dot = name.IndexOf('.');
            if (dot > 0 && int.TryParse(name[..dot], out int id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
