using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Drives the on-screen avatar: loads an animation library and transitions the displayed frame set
/// as the AI's semantic <see cref="AgentState"/> changes (Graphics Engine; implementation_plan.md,
/// Phase 6).
/// </summary>
public interface IGraphicsEngine
{
    Task LoadAsync(AnimationLibrary library, CancellationToken cancellationToken);

    void TransitionTo(AgentState state);
}
