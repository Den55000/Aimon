using System.IO.Compression;
using Aimon.Application.Abstractions;
using Aimon.Application.Profiles;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Infrastructure.Persistence;

/// <summary>
/// File-system implementation of <see cref="IProfileStore"/>. Each profile is a folder under the
/// configured root containing <c>profile.json</c> and, when there are safety blocks,
/// <c>hard-settings.json</c> (implementation_plan.md, §3.5). Export/import move the folder in and out
/// of a single zip bundle.
/// </summary>
internal sealed class FileProfileStore : IProfileStore
{
    private const string StagingPrefix = ".import-";

    private readonly ProfileStoreOptions _options;

    public FileProfileStore(ProfileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<Profile?> GetAsync(ProfileId id, CancellationToken cancellationToken)
    {
        var directory = DirectoryFor(id);
        var profilePath = Path.Combine(directory, ProfileSchema.ProfileFileName);
        if (!File.Exists(profilePath))
            return null;

        var profileJson = await File.ReadAllTextAsync(profilePath, cancellationToken).ConfigureAwait(false);

        var hardSettingsPath = Path.Combine(directory, ProfileSchema.HardSettingsFileName);
        var hardSettingsJson = File.Exists(hardSettingsPath)
            ? await File.ReadAllTextAsync(hardSettingsPath, cancellationToken).ConfigureAwait(false)
            : null;

        return ProfileSerializer.DeserializeProfile(profileJson, hardSettingsJson);
    }

    public async Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_options.RootDirectory))
            return [];

        var summaries = new List<ProfileSummary>();
        foreach (var directory in Directory.EnumerateDirectories(_options.RootDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var profilePath = Path.Combine(directory, ProfileSchema.ProfileFileName);
            if (!File.Exists(profilePath))
                continue;

            var json = await File.ReadAllTextAsync(profilePath, cancellationToken).ConfigureAwait(false);
            var document = ProfileSerializer.DeserializeDocument(json);
            summaries.Add(new ProfileSummary
            {
                Id = ProfileId.Parse(document.Id),
                Name = document.Name,
                UpdatedAt = document.UpdatedAt,
            });
        }

        return summaries;
    }

    public async Task SaveAsync(Profile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var directory = DirectoryFor(profile.Id);
        Directory.CreateDirectory(directory);

        var profilePath = Path.Combine(directory, ProfileSchema.ProfileFileName);
        await File.WriteAllTextAsync(profilePath, ProfileSerializer.SerializeProfile(profile), cancellationToken).ConfigureAwait(false);

        var hardSettingsPath = Path.Combine(directory, ProfileSchema.HardSettingsFileName);
        if (HasBlocks(profile.HardSettings))
            await File.WriteAllTextAsync(hardSettingsPath, ProfileSerializer.SerializeHardSettings(profile.HardSettings), cancellationToken).ConfigureAwait(false);
        else if (File.Exists(hardSettingsPath))
            File.Delete(hardSettingsPath);
    }

    public Task DeleteAsync(ProfileId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = DirectoryFor(id);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);

        return Task.CompletedTask;
    }

    public Task ExportAsync(ProfileId id, string bundlePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = DirectoryFor(id);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"No profile found for id '{id}'.");

        if (File.Exists(bundlePath))
            File.Delete(bundlePath);

        var bundleDirectory = Path.GetDirectoryName(bundlePath);
        if (!string.IsNullOrEmpty(bundleDirectory))
            Directory.CreateDirectory(bundleDirectory);

        ZipFile.CreateFromDirectory(directory, bundlePath);
        return Task.CompletedTask;
    }

    public async Task<Profile> ImportAsync(string bundlePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("Profile bundle not found.", bundlePath);

        Directory.CreateDirectory(_options.RootDirectory);

        // Stage inside the root so the final move is a same-volume rename.
        var staging = Path.Combine(_options.RootDirectory, StagingPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(bundlePath, staging);

            var profilePath = Path.Combine(staging, ProfileSchema.ProfileFileName);
            if (!File.Exists(profilePath))
                throw new InvalidDataException("Bundle does not contain a profile.json.");

            var document = ProfileSerializer.DeserializeDocument(
                await File.ReadAllTextAsync(profilePath, cancellationToken).ConfigureAwait(false));
            var id = ProfileId.Parse(document.Id);

            var destination = DirectoryFor(id);
            if (Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);

            Directory.Move(staging, destination);

            var imported = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            return imported ?? throw new InvalidDataException("Imported profile could not be read back.");
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private string DirectoryFor(ProfileId id) => Path.Combine(_options.RootDirectory, id.ToString());

    private static bool HasBlocks(HardSettings hardSettings)
        => hardSettings.BlockedTopics.Count > 0
           || hardSettings.BlockedModels.Count > 0
           || hardSettings.BlockedActions.Count > 0;
}
