using Aimon.Application.Authoring;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Compiles a natural-language persona description into a structured profile draft, and refines an
/// existing draft when given a revision instruction (Profile Compiler; implementation_plan.md,
/// Phase 5). The draft is reviewed/confirmed before it is persisted.
/// </summary>
public interface IProfileCompiler
{
    Task<StructuredProfile> CompileAsync(ProfileCompilationRequest request, CancellationToken cancellationToken);
}
