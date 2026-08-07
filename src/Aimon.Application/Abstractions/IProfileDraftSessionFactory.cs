using Aimon.Application.Authoring;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Begins a Profile Compiler authoring session: compiles the first draft from a natural-language
/// description and hands back a <see cref="ProfileDraftSession"/> to drive the review/confirm loop
/// (implementation_plan.md, Phase 5).
/// </summary>
public interface IProfileDraftSessionFactory
{
    Task<ProfileDraftSession> BeginAsync(string name, string naturalLanguageDescription, CancellationToken cancellationToken);
}
