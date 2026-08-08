using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Drives the on-screen avatar: loads an animation library, switches the active frame set as the AI's
/// semantic <see cref="AgentState"/> changes, and steps through that set's frames so a renderer can
/// display the current one (Graphics Engine; implementation_plan.md, Phase 6). The frame-advance
/// clock (fps → wall-clock ticks) lives in the renderer, keeping this logic deterministic and testable.
/// </summary>
public interface IGraphicsEngine
{
    Task LoadAsync(AnimationLibrary library, CancellationToken cancellationToken);

    /// <summary>Switches to the frame set for <paramref name="state"/> (falling back to Idle) and restarts it.</summary>
    void TransitionTo(AgentState state);

    /// <summary>Advances to the next frame of the active set, looping or holding per the set's settings.</summary>
    void Advance();

    AgentState CurrentState { get; }

    FrameSet? CurrentFrameSet { get; }

    /// <summary>The active frame's reference, or <c>null</c> when no frames are available.</summary>
    string? CurrentFrame { get; }

    /// <summary>Zero-based index of the active frame within <see cref="CurrentFrameSet"/>.</summary>
    int CurrentFrameIndex { get; }

    /// <summary>Playback rate of the active frame set (frames per second) for the renderer's clock.</summary>
    int FramesPerSecond { get; }

    /// <summary>Raised when the active frame changes — on a transition or an advance.</summary>
    event EventHandler? FrameChanged;
}
