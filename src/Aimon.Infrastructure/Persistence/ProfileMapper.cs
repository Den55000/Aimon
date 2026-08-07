using Aimon.Domain.Graphics;
using Aimon.Domain.Models;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Infrastructure.Persistence;

/// <summary>
/// Maps between the Domain <see cref="Profile"/> model and the on-disk documents. Loading goes
/// through the Domain value objects' constructors, so an invalid file surfaces as a domain
/// validation error rather than a silently-broken profile.
/// </summary>
internal static class ProfileMapper
{
    public static ProfileDocument ToDocument(Profile profile) => new()
    {
        SchemaVersion = profile.SchemaVersion,
        Id = profile.Id.ToString(),
        Name = profile.Name,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
        NaturalLanguageSource = profile.NaturalLanguageSource,
        Structured = ToDocument(profile.Structured),
        Graphics = new GraphicsDocument { AnimationLibraryId = profile.Graphics.AnimationLibraryId },
    };

    public static Profile ToDomain(ProfileDocument document, HardSettings hardSettings) => new()
    {
        Id = ProfileId.Parse(document.Id),
        Name = document.Name,
        SchemaVersion = document.SchemaVersion,
        NaturalLanguageSource = document.NaturalLanguageSource,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
        Structured = ToDomain(document.Structured),
        HardSettings = hardSettings,
        Graphics = new GraphicsConfiguration { AnimationLibraryId = document.Graphics.AnimationLibraryId },
    };

    public static HardSettingsDocument ToDocument(HardSettings hardSettings) => new()
    {
        BlockedTopics = [.. hardSettings.BlockedTopics],
        BlockedModels = [.. hardSettings.BlockedModels],
        BlockedActions = [.. hardSettings.BlockedActions],
    };

    public static HardSettings ToDomain(HardSettingsDocument document)
        => new(document.BlockedTopics, document.BlockedModels, document.BlockedActions);

    private static StructuredProfileDocument ToDocument(StructuredProfile structured) => new()
    {
        Personality = new PersonalityDocument
        {
            Persona = structured.Personality.Persona,
            Tone = structured.Personality.Tone,
            Traits = [.. structured.Personality.Traits],
        },
        InteractionRules = new InteractionRulesDocument
        {
            Dos = [.. structured.InteractionRules.Dos],
            Donts = [.. structured.InteractionRules.Donts],
            Verbosity = structured.InteractionRules.Verbosity,
            FormattingPreference = structured.InteractionRules.FormattingPreference,
        },
        ModelPreferences = new ModelPreferencesDocument
        {
            Preferred = [.. structured.ModelPreferences.Preferred.Select(ToDocument)],
            DefaultParameters = ToDocument(structured.ModelPreferences.DefaultParameters),
        },
        TaskStyles = [.. structured.TaskStyles.Select(ToDocument)],
    };

    private static StructuredProfile ToDomain(StructuredProfileDocument document) => new()
    {
        Personality = new Personality
        {
            Persona = document.Personality.Persona,
            Tone = document.Personality.Tone,
            Traits = [.. document.Personality.Traits],
        },
        InteractionRules = new InteractionRules
        {
            Dos = [.. document.InteractionRules.Dos],
            Donts = [.. document.InteractionRules.Donts],
            Verbosity = document.InteractionRules.Verbosity,
            FormattingPreference = document.InteractionRules.FormattingPreference,
        },
        ModelPreferences = new ModelPreferences
        {
            Preferred = [.. document.ModelPreferences.Preferred.Select(ToDomain)],
            DefaultParameters = ToDomain(document.ModelPreferences.DefaultParameters),
        },
        TaskStyles = [.. document.TaskStyles.Select(ToDomain)],
    };

    private static ModelRefDocument ToDocument(ModelRef model)
        => new() { Provider = model.Provider, Model = model.Model };

    private static ModelRef ToDomain(ModelRefDocument document)
        => new(document.Provider, document.Model);

    private static GenerationParametersDocument ToDocument(GenerationParameters parameters) => new()
    {
        Temperature = parameters.Temperature,
        TopP = parameters.TopP,
        MaxOutputTokens = parameters.MaxOutputTokens,
    };

    private static GenerationParameters ToDomain(GenerationParametersDocument document)
        => new(document.Temperature, document.TopP, document.MaxOutputTokens);

    private static TaskStyleDocument ToDocument(TaskStyle taskStyle) => new()
    {
        TaskName = taskStyle.TaskName,
        SystemPromptAddition = taskStyle.SystemPromptAddition,
        ModelOverride = taskStyle.ModelOverride is null ? null : ToDocument(taskStyle.ModelOverride),
        ParametersOverride = taskStyle.ParametersOverride is null ? null : ToDocument(taskStyle.ParametersOverride),
    };

    private static TaskStyle ToDomain(TaskStyleDocument document) => new()
    {
        TaskName = document.TaskName,
        SystemPromptAddition = document.SystemPromptAddition,
        ModelOverride = document.ModelOverride is null ? null : ToDomain(document.ModelOverride),
        ParametersOverride = document.ParametersOverride is null ? null : ToDomain(document.ParametersOverride),
    };
}
