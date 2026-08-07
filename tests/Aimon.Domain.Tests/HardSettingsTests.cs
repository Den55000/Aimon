using Aimon.Domain.Safety;

namespace Aimon.Domain.Tests;

public class HardSettingsTests
{
    [Fact]
    public void None_Is_Empty()
    {
        Assert.Empty(HardSettings.None.BlockedModels);
        Assert.Empty(HardSettings.None.BlockedTopics);
        Assert.Empty(HardSettings.None.BlockedActions);
    }

    [Fact]
    public void Normalizes_Trims_Blanks_And_Duplicates()
    {
        var settings = new HardSettings(blockedModels: [" gpt-4o ", "gpt-4o", "  ", "", "GPT-4O"]);

        Assert.Single(settings.BlockedModels);
        Assert.Equal("gpt-4o", settings.BlockedModels[0]);
    }

    [Fact]
    public void IsModelBlocked_Is_Case_Insensitive_And_Trims_The_Candidate()
    {
        var settings = new HardSettings(blockedModels: ["gpt-4o"]);

        Assert.True(settings.IsModelBlocked("GPT-4o"));
        Assert.True(settings.IsModelBlocked("  gpt-4o  "));
        Assert.False(settings.IsModelBlocked("gpt-4o-mini"));
        Assert.False(settings.IsModelBlocked("   "));
    }
}
