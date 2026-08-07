using Aimon.Domain.Graphics;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Packages user-authored frames/sketches into an <see cref="AnimationLibrary"/> keyed by
/// AgentState (Graphics Compiler; implementation_plan.md, Phase 6).
/// </summary>
public interface IGraphicsCompiler
{
    Task<AnimationLibrary> CompileAsync(string sourceDirectory, CancellationToken cancellationToken);
}
