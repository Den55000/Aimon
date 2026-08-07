using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class ProfileSerializerTests
{
    [Fact]
    public void Serialization_Is_Stable_And_Lossless()
    {
        var original = TestProfiles.CreateSample();

        var json = ProfileSerializer.SerializeProfile(original);
        var hardSettingsJson = ProfileSerializer.SerializeHardSettings(original.HardSettings);
        var roundTripped = ProfileSerializer.DeserializeProfile(json, hardSettingsJson);

        // Re-serializing the round-tripped profile must produce byte-identical JSON.
        Assert.Equal(json, ProfileSerializer.SerializeProfile(roundTripped));
        Assert.Equal(hardSettingsJson, ProfileSerializer.SerializeHardSettings(roundTripped.HardSettings));
    }

    [Fact]
    public void Serialized_Profile_Uses_CamelCase_And_String_Enums()
    {
        var json = ProfileSerializer.SerializeProfile(TestProfiles.CreateSample());

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"naturalLanguageSource\"", json);
        Assert.Contains("\"verbosity\": \"Detailed\"", json);
    }
}
