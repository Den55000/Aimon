using Aimon.Domain.Profiles;

namespace Aimon.Infrastructure.Persistence;

// On-disk serialization DTOs for the portable profile format (implementation_plan.md, §3.5).
// These are deliberately decoupled from the Domain model so the file schema can evolve
// independently and stay forward-compatible. Domain value objects (with their invariants) are
// reconstructed by ProfileMapper on load.

/// <summary>Root document persisted as <c>profile.json</c>.</summary>
internal sealed class ProfileDocument
{
    public int SchemaVersion { get; set; } = ProfileSchema.CurrentVersion;

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string NaturalLanguageSource { get; set; } = "";

    public StructuredProfileDocument Structured { get; set; } = new();

    public GraphicsDocument Graphics { get; set; } = new();
}

internal sealed class StructuredProfileDocument
{
    public PersonalityDocument Personality { get; set; } = new();

    public InteractionRulesDocument InteractionRules { get; set; } = new();

    public ModelPreferencesDocument ModelPreferences { get; set; } = new();

    public List<TaskStyleDocument> TaskStyles { get; set; } = [];
}

internal sealed class PersonalityDocument
{
    public string Persona { get; set; } = "";

    public string Tone { get; set; } = "";

    public List<string> Traits { get; set; } = [];
}

internal sealed class InteractionRulesDocument
{
    public List<string> Dos { get; set; } = [];

    public List<string> Donts { get; set; } = [];

    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    public string? FormattingPreference { get; set; }
}

internal sealed class ModelRefDocument
{
    public string Provider { get; set; } = "";

    public string Model { get; set; } = "";
}

internal sealed class GenerationParametersDocument
{
    public double Temperature { get; set; } = 1.0;

    public double TopP { get; set; } = 1.0;

    public int? MaxOutputTokens { get; set; }
}

internal sealed class ModelPreferencesDocument
{
    public List<ModelRefDocument> Preferred { get; set; } = [];

    public GenerationParametersDocument DefaultParameters { get; set; } = new();
}

internal sealed class TaskStyleDocument
{
    public string TaskName { get; set; } = "";

    public string? SystemPromptAddition { get; set; }

    public ModelRefDocument? ModelOverride { get; set; }

    public GenerationParametersDocument? ParametersOverride { get; set; }
}

internal sealed class GraphicsDocument
{
    public string? AnimationLibraryId { get; set; }
}

/// <summary>Optional document persisted as <c>hard-settings.json</c>.</summary>
internal sealed class HardSettingsDocument
{
    public List<string> BlockedTopics { get; set; } = [];

    public List<string> BlockedModels { get; set; } = [];

    public List<string> BlockedActions { get; set; } = [];
}
