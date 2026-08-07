namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>Locates repository files (e.g. bundled samples) relative to the test output directory.</summary>
internal static class RepoLayout
{
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aimon.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root (Aimon.slnx) from '{AppContext.BaseDirectory}'.");
    }
}
