using Aimon.Domain.Models;

namespace Aimon.Domain.Profiles;

/// <summary>
/// Per-task overrides layered on top of the base profile — e.g. a different system-prompt addition,
/// model, or generation parameters when the user is doing a particular kind of work.
/// </summary>
public sealed record TaskStyle
{
    public required string TaskName { get; init; }

    public string? SystemPromptAddition { get; init; }

    public ModelRef? ModelOverride { get; init; }

    public GenerationParameters? ParametersOverride { get; init; }
}
