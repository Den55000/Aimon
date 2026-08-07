namespace Aimon.Infrastructure.Persistence;

/// <summary>Configures where <see cref="FileProfileStore"/> keeps profiles on disk.</summary>
internal sealed class ProfileStoreOptions
{
    /// <summary>Root folder that holds one sub-folder per profile.</summary>
    public required string RootDirectory { get; init; }

    /// <summary>Default location under the current user's application-data folder.</summary>
    public static ProfileStoreOptions Default() => new()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aimon",
            "profiles"),
    };
}
