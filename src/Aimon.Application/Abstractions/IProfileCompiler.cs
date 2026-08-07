using Aimon.Domain.Profiles;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Compiles a natural-language persona description into a structured profile draft (Profile
/// Compiler; implementation_plan.md, Phase 5). The draft is reviewed/confirmed before it is persisted.
/// </summary>
public interface IProfileCompiler
{
    Task<StructuredProfile> CompileAsync(string naturalLanguageDescription, CancellationToken cancellationToken);
}
