using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Application.Graphics;

/// <summary>
/// Default <see cref="IGraphicsEngine"/>: a deterministic frame-per-state player. It selects the frame
/// set for the current <see cref="AgentState"/> (falling back to Idle when a state is unmapped) and
/// steps through its frames on demand — looping, or holding the last frame for one-shot sets. All
/// timing is external, so the sequencing can be unit-tested without a UI or a clock
/// (implementation_plan.md, Phase 6).
/// </summary>
public sealed class FrameSequencer : IGraphicsEngine
{
    private const int DefaultFramesPerSecond = 12;

    private AnimationLibrary? _library;

    public AgentState CurrentState { get; private set; } = AgentState.Idle;

    public FrameSet? CurrentFrameSet { get; private set; }

    public int CurrentFrameIndex { get; private set; }

    public string? CurrentFrame
        => CurrentFrameSet is { Frames.Count: > 0 } frameSet ? frameSet.Frames[CurrentFrameIndex] : null;

    public int FramesPerSecond => CurrentFrameSet?.FramesPerSecond ?? DefaultFramesPerSecond;

    public event EventHandler? FrameChanged;

    public Task LoadAsync(AnimationLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        _library = library;
        SelectFrameSet(CurrentState);
        FrameChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void TransitionTo(AgentState state)
    {
        CurrentState = state;
        SelectFrameSet(state);
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Advance()
    {
        if (CurrentFrameSet is not { Frames.Count: > 0 } frameSet)
            return;

        if (CurrentFrameIndex + 1 < frameSet.Frames.Count)
            CurrentFrameIndex++;
        else if (frameSet.Loop)
            CurrentFrameIndex = 0;
        else
            return; // One-shot set: hold on the final frame.

        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    // Point at the state's frame set, or Idle's if the state has none, and restart it from frame 0.
    private void SelectFrameSet(AgentState state)
    {
        CurrentFrameIndex = 0;
        CurrentFrameSet = _library?.GetFrameSet(state) ?? _library?.GetFrameSet(AgentState.Idle);
    }
}
