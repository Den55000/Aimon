using Aimon.Domain.Graphics;
using Aimon.Domain.Models;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>Builds a fully-populated profile so serialization/round-trip tests exercise every field.</summary>
internal static class TestProfiles
{
    public static Profile CreateSample(ProfileId? id = null) => new()
    {
        Id = id ?? ProfileId.New(),
        Name = "Friendly Assistant",
        NaturalLanguageSource = "A warm, concise assistant that helps with coding and email.",
        CreatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 1, 9, 30, 0, TimeSpan.Zero),
        Structured = new StructuredProfile
        {
            Personality = new Personality
            {
                Persona = "Warm and encouraging",
                Tone = "friendly, concise",
                Traits = ["patient", "curious"],
            },
            InteractionRules = new InteractionRules
            {
                Dos = ["explain reasoning"],
                Donts = ["use jargon"],
                Verbosity = Verbosity.Detailed,
                FormattingPreference = "markdown",
            },
            ModelPreferences = new ModelPreferences
            {
                Preferred = [new ModelRef("openai", "gpt-4o"), new ModelRef("openai", "gpt-4o-mini")],
                DefaultParameters = new GenerationParameters(temperature: 0.4, topP: 0.95, maxOutputTokens: 1024),
            },
            TaskStyles =
            [
                new TaskStyle
                {
                    TaskName = "coding",
                    SystemPromptAddition = "Prefer C#.",
                    ModelOverride = new ModelRef("openai", "gpt-4o"),
                    ParametersOverride = new GenerationParameters(temperature: 0.2),
                },
                new TaskStyle { TaskName = "email", SystemPromptAddition = "Keep it brief." },
            ],
        },
        HardSettings = new HardSettings(
            blockedTopics: ["politics"],
            blockedModels: ["gpt-3.5"],
            blockedActions: ["delete-files"]),
        Graphics = new GraphicsConfiguration { AnimationLibraryId = "default" },
    };
}
