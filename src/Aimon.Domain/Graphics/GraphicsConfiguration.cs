namespace Aimon.Domain.Graphics;

/// <summary>
/// A profile's graphics settings: which animation library to use for the avatar. Kept minimal for
/// the staged "reactions first" approach (implementation_plan.md, D1); per-state overrides come later.
/// </summary>
public sealed record GraphicsConfiguration
{
    /// <summary>Identifier of the animation library to load; <c>null</c> means the built-in default.</summary>
    public string? AnimationLibraryId { get; init; }

    /// <summary>Use the built-in default animation library.</summary>
    public static GraphicsConfiguration Default { get; } = new();
}
