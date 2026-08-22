using System.Text;

namespace Cronus.Server.Host;

/// <summary>
/// Mirrors everything written to the console into a per-run log file, so the server always
/// leaves a log regardless of how it was launched (`dotnet run`, double-click, service, …) —
/// the rehearsal/ops story is "send the newest file in logs/". Disabled with CRONUS_LOG_DIR=0;
/// CRONUS_LOG_DIR=&lt;path&gt; relocates it (default: <c>logs/</c> next to the executable).
/// Old runs are pruned beyond the newest 20 files.
/// </summary>
public static class TeeLog
{
    private const int KeepFiles = 20;

    public static string? Attach()
    {
        string? dirSetting = Environment.GetEnvironmentVariable("CRONUS_LOG_DIR");
        if (dirSetting is "0" or "off" or "false")
        {
            return null;
        }

        try
        {
            string dir = string.IsNullOrWhiteSpace(dirSetting)
                ? Path.Combine(AppContext.BaseDirectory, "logs")
                : dirSetting;
            Directory.CreateDirectory(dir);

            Prune(dir);

            string path = Path.Combine(dir, $"cronus-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var file = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(new TeeWriter(Console.Out, file));
            return path;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[log] file log unavailable ({ex.Message}); console only.");
            return null;
        }
    }

    private static void Prune(string dir)
    {
        var old = new DirectoryInfo(dir).GetFiles("cronus-*.log")
            .OrderByDescending(f => f.Name)
            .Skip(KeepFiles - 1);
        foreach (FileInfo file in old)
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
                // a viewer has it open; it'll go next time
            }
        }
    }

    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _file;

        public TeeWriter(TextWriter console, TextWriter file)
        {
            _console = console;
            _file = file;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            _console.Write(value);
            _file.Write(value);
        }

        public override void Write(string? value)
        {
            _console.Write(value);
            _file.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _console.WriteLine(value);
            _file.WriteLine(value);
        }

        public override void Flush()
        {
            _console.Flush();
            _file.Flush();
        }
    }
}
