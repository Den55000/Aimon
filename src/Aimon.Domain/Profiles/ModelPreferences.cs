using Aimon.Domain.Models;

namespace Aimon.Domain.Profiles;

/// <summary>A profile's preferred models (in priority order) and default generation parameters.</summary>
public sealed record ModelPreferences
{
    public IReadOnlyList<ModelRef> Preferred { get; init; } = [];

    public GenerationParameters DefaultParameters { get; init; } = GenerationParameters.Default;

    /// <summary>No preferred models; neutral default parameters.</summary>
    public static ModelPreferences Empty { get; } = new();
}
