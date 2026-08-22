using Cronus.Common;
using Xunit;

namespace Cronus.Network.Tests;

/// <summary>The dotenv loader: KEY=VALUE parsing, comments, quotes, and precedence
/// (real environment variables beat the file).</summary>
public sealed class DotEnvTests
{
    [Fact]
    public void LoadFile_ParsesEntries_SkipsCommentsAndBlanks_StripsQuotes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cronus-env-{Guid.NewGuid()}.env");
        string keyA = $"CRONUS_TEST_A_{Guid.NewGuid():N}";
        string keyB = $"CRONUS_TEST_B_{Guid.NewGuid():N}";
        string keyC = $"CRONUS_TEST_C_{Guid.NewGuid():N}";
        File.WriteAllLines(path, new[]
        {
            "# comment line",
            "",
            $"{keyA}=plain value",
            $"{keyB}=\"double quoted\"",
            $"  {keyC} = 'single quoted' ",
            "NOT_A_PAIR",
            "=novalue",
        });

        try
        {
            DotEnv.LoadFile(path);

            Assert.Equal("plain value", Environment.GetEnvironmentVariable(keyA));
            Assert.Equal("double quoted", Environment.GetEnvironmentVariable(keyB));
            Assert.Equal("single quoted", Environment.GetEnvironmentVariable(keyC));
        }
        finally
        {
            File.Delete(path);
            Environment.SetEnvironmentVariable(keyA, null);
            Environment.SetEnvironmentVariable(keyB, null);
            Environment.SetEnvironmentVariable(keyC, null);
        }
    }

    [Fact]
    public void LoadFile_DoesNotOverrideARealEnvironmentVariable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cronus-env-{Guid.NewGuid()}.env");
        string key = $"CRONUS_TEST_WIN_{Guid.NewGuid():N}";
        File.WriteAllLines(path, new[] { $"{key}=from-file" });
        Environment.SetEnvironmentVariable(key, "from-environment");

        try
        {
            DotEnv.LoadFile(path);
            Assert.Equal("from-environment", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            File.Delete(path);
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
