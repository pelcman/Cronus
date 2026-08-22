namespace Cronus.Common;

/// <summary>
/// Loads a dotenv-style <c>.env</c> file into the process environment (the Maple2 approach to
/// configuration: one file at the repo root instead of a wall of <c>export</c>s). Format:
/// <c>KEY=VALUE</c> lines; blank lines and <c>#</c> comments are skipped; surrounding single or
/// double quotes on the value are stripped. Variables already present in the real environment
/// win over the file, so a deployment can still override any entry.
/// </summary>
public static class DotEnv
{
    /// <summary>
    /// Finds and loads the nearest <c>.env</c>: the current directory, then up to 6 parents (so
    /// running from the repo root, a project directory, or the build-output directory all find the
    /// repo-root file), and finally the executable's own directory. Returns the loaded file's path,
    /// or null when there is none.
    /// </summary>
    public static string? Load()
    {
        string? path = Find();
        if (path is not null)
        {
            LoadFile(path);
        }

        return path;
    }

    /// <summary>Applies one specific <c>.env</c> file (existing environment variables win).</summary>
    public static void LoadFile(string path)
    {
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            if (value.Length >= 2
                && ((value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? Find()
    {
        string? dir = Directory.GetCurrentDirectory();
        for (int i = 0; i <= 6 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        string exeCandidate = Path.Combine(AppContext.BaseDirectory, ".env");
        return File.Exists(exeCandidate) ? exeCandidate : null;
    }
}
