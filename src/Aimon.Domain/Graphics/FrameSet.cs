namespace Aimon.Domain.Graphics;

/// <summary>
/// The frames the Graphics Engine plays for one <see cref="AgentState"/>: an ordered list of frame
/// references plus playback settings.
/// </summary>
public sealed record FrameSet
{
    public required AgentState State { get; init; }

    /// <summary>Frame references (relative paths within the animation library), in playback order.</summary>
    public IReadOnlyList<string> Frames { get; init; } = [];

    public int FramesPerSecond { get; init; } = 12;

    public bool Loop { get; init; } = true;
}
