namespace Aimon.Domain.Graphics;

/// <summary>
/// A named, versioned collection of <see cref="FrameSet"/>s — one per <see cref="AgentState"/> —
/// produced by the Graphics Compiler and consumed by the Graphics Engine.
/// </summary>
public sealed record AnimationLibrary
{
    public required string Name { get; init; }

    public string Version { get; init; } = "1.0.0";

    public IReadOnlyList<FrameSet> FrameSets { get; init; } = [];

    /// <summary>Returns the frame set for <paramref name="state"/>, or <c>null</c> if none is defined.</summary>
    public FrameSet? GetFrameSet(AgentState state)
        => FrameSets.FirstOrDefault(frameSet => frameSet.State == state);
}
