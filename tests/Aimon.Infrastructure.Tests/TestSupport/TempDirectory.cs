namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>A throwaway directory that deletes itself on dispose. Used to isolate file-store tests.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "aimon-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a locked file should not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Ditto.
        }
    }
}
