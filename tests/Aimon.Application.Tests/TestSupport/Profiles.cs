using Aimon.Domain.Models;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Application.Tests.TestSupport;

internal static class TestProfiles
{
    public static Profile WithModel(string model = "gpt-4o", HardSettings? hardSettings = null) => new()
    {
        Id = ProfileId.New(),
        Name = "Test",
        Structured = new StructuredProfile
        {
            Personality = new Personality { Persona = "a test assistant" },
            ModelPreferences = new ModelPreferences { Preferred = [new ModelRef("openai", model)] },
        },
        HardSettings = hardSettings ?? HardSettings.None,
    };

    public static Profile WithoutModel() => new()
    {
        Id = ProfileId.New(),
        Name = "No model",
    };
}
