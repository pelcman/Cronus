using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cronus.Debug.Bot;

/// <summary>
/// Launches N real JMS v186 client windows (the localhost-patched client) so the content the
/// bots exercise is also visible on screen. Best-effort: on a non-Windows host or when the
/// client isn't found, it logs and the bots still run.
/// </summary>
public static class ClientLauncher
{
    /// <summary>
    /// Resolves the client executable: CRONUS_CLIENT_PATH if set, else the repo's bundled
    /// localhost client (searched upward from the working directory).
    /// </summary>
    public static string? ResolveClientPath()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("CRONUS_CLIENT_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        // Search upward for the bundled localhost-patched client. Prefer MapleStory_v186 — it
        // has the full wz game data; the loacalhost_client folder ships only the exe/installer.
        string[] candidates =
        {
            Path.Combine("Client", "MapleStory_v186", "JMS_v186.1_L.exe"),
            Path.Combine("..", "Client", "MapleStory_v186", "JMS_v186.1_L.exe"),
            Path.Combine("Client", "loacalhost_client", "JMS_v186.1_L.exe"),
            Path.Combine("..", "Client", "loacalhost_client", "JMS_v186.1_L.exe"),
        };

        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        for (int depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            foreach (string rel in candidates)
            {
                string full = Path.GetFullPath(Path.Combine(dir.FullName, rel));
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    /// <summary>Launches <paramref name="count"/> client windows; returns the started processes.</summary>
    public static IReadOnlyList<Process> Launch(int count, string clientPath)
    {
        var started = new List<Process>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("[launcher] not on Windows — skipping real client windows.");
            return started;
        }

        string workingDir = Path.GetDirectoryName(clientPath) ?? Directory.GetCurrentDirectory();
        for (int i = 0; i < count; i++)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = clientPath,
                    WorkingDirectory = workingDir,
                    UseShellExecute = true, // the anti-cheat client expects a normal shell launch
                };
                Process? p = Process.Start(psi);
                if (p is not null)
                {
                    started.Add(p);
                    Console.WriteLine($"[launcher] started client window {i + 1} (pid {p.Id}).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[launcher] client window {i + 1} failed: {ex.Message}");
            }
        }

        return started;
    }
}
