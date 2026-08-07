using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>Guards the bundled sample in <c>samples/</c> against drifting from the current format.</summary>
public class SampleProfileTests
{
    [Fact]
    public void Bundled_Sample_Profile_Loads_Through_The_Real_Serializer()
    {
        var repositoryRoot = RepoLayout.FindRepositoryRoot();
        var directory = Path.Combine(repositoryRoot, "samples", "profiles", "friendly-assistant");

        var profileJson = File.ReadAllText(Path.Combine(directory, "profile.json"));
        var hardSettingsJson = File.ReadAllText(Path.Combine(directory, "hard-settings.json"));

        var profile = ProfileSerializer.DeserializeProfile(profileJson, hardSettingsJson);

        Assert.Equal("Friendly Assistant", profile.Name);
        Assert.Equal(2, profile.Structured.ModelPreferences.Preferred.Count);
        Assert.True(profile.HardSettings.IsModelBlocked("gpt-3.5"));
        Assert.Equal("default", profile.Graphics.AnimationLibraryId);
    }
}
