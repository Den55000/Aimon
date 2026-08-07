using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;
using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class FileProfileStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Save_Then_Get_Round_Trips_Every_Field()
    {
        using var root = new TempDirectory();
        var store = new FileProfileStore(new ProfileStoreOptions { RootDirectory = root.Root });
        var original = TestProfiles.CreateSample();

        await store.SaveAsync(original, Ct);
        var loaded = await store.GetAsync(original.Id, Ct);

        Assert.NotNull(loaded);
        Assert.Equal(ProfileSerializer.SerializeProfile(original), ProfileSerializer.SerializeProfile(loaded));
        Assert.Equal("Friendly Assistant", loaded.Name);
        Assert.True(loaded.HardSettings.IsModelBlocked("gpt-3.5"));
        Assert.Equal(2, loaded.Structured.TaskStyles.Count);
    }

    [Fact]
    public async Task Get_Returns_Null_When_Profile_Does_Not_Exist()
    {
        using var root = new TempDirectory();
        var store = new FileProfileStore(new ProfileStoreOptions { RootDirectory = root.Root });

        Assert.Null(await store.GetAsync(ProfileId.New(), Ct));
    }

    [Fact]
    public async Task List_Returns_A_Summary_Per_Saved_Profile()
    {
        using var root = new TempDirectory();
        var store = new FileProfileStore(new ProfileStoreOptions { RootDirectory = root.Root });
        var first = TestProfiles.CreateSample();
        var second = TestProfiles.CreateSample();

        await store.SaveAsync(first, Ct);
        await store.SaveAsync(second, Ct);
        var summaries = await store.ListAsync(Ct);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, summary => summary.Id == first.Id);
        Assert.Contains(summaries, summary => summary.Id == second.Id);
    }

    [Fact]
    public async Task Delete_Removes_The_Profile()
    {
        using var root = new TempDirectory();
        var store = new FileProfileStore(new ProfileStoreOptions { RootDirectory = root.Root });
        var profile = TestProfiles.CreateSample();

        await store.SaveAsync(profile, Ct);
        await store.DeleteAsync(profile.Id, Ct);

        Assert.Null(await store.GetAsync(profile.Id, Ct));
        Assert.Empty(await store.ListAsync(Ct));
    }

    [Fact]
    public async Task Save_Without_Hard_Settings_Writes_No_Hard_Settings_File()
    {
        using var root = new TempDirectory();
        var store = new FileProfileStore(new ProfileStoreOptions { RootDirectory = root.Root });
        var profile = TestProfiles.CreateSample() with { HardSettings = HardSettings.None };

        await store.SaveAsync(profile, Ct);

        var hardSettingsPath = Path.Combine(root.Root, profile.Id.ToString(), "hard-settings.json");
        Assert.False(File.Exists(hardSettingsPath));

        var loaded = await store.GetAsync(profile.Id, Ct);
        Assert.NotNull(loaded);
        Assert.Empty(loaded.HardSettings.BlockedModels);
    }

    [Fact]
    public async Task Export_Then_Import_Into_A_Clean_Store_Reproduces_The_Profile()
    {
        using var sourceRoot = new TempDirectory();
        using var destinationRoot = new TempDirectory();
        using var bundleRoot = new TempDirectory();

        var source = new FileProfileStore(new ProfileStoreOptions { RootDirectory = sourceRoot.Root });
        var destination = new FileProfileStore(new ProfileStoreOptions { RootDirectory = destinationRoot.Root });
        var profile = TestProfiles.CreateSample();
        var bundlePath = Path.Combine(bundleRoot.Root, "friendly-assistant.aimon.zip");

        await source.SaveAsync(profile, Ct);
        await source.ExportAsync(profile.Id, bundlePath, Ct);
        var imported = await destination.ImportAsync(bundlePath, Ct);

        Assert.True(File.Exists(bundlePath));
        Assert.Equal(profile.Id, imported.Id);

        var reloaded = await destination.GetAsync(profile.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal(ProfileSerializer.SerializeProfile(profile), ProfileSerializer.SerializeProfile(reloaded));
    }
}
