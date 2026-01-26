namespace Castr.Tests.TestHelpers;

public static class TestDatabaseHelper
{
    public static string CreateTempDatabase()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"castr_test_{Guid.NewGuid()}.db");
        return tempPath;
    }

    public static string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"castr_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    public static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
