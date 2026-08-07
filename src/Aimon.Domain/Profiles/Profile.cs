using Aimon.Domain.Graphics;
using Aimon.Domain.Safety;

namespace Aimon.Domain.Profiles;

/// <summary>
/// A personalization profile: the user's natural-language source, the compiled structured fields,
/// optional hard settings, and graphics configuration. Portable and versioned
/// (implementation_plan.md, §3.5).
/// </summary>
public sealed record Profile
{
    public required ProfileId Id { get; init; }

    public required string Name { get; init; }

    public int SchemaVersion { get; init; } = 1;

    /// <summary>The plain-language description the user authored (or dictated to the compiler).</summary>
    public string NaturalLanguageSource { get; init; } = "";

    public StructuredProfile Structured { get; init; } = StructuredProfile.Empty;

    public HardSettings HardSettings { get; init; } = HardSettings.None;

    public GraphicsConfiguration Graphics { get; init; } = GraphicsConfiguration.Default;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
