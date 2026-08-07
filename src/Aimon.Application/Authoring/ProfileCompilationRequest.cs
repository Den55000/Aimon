using Aimon.Domain.Profiles;

namespace Aimon.Application.Authoring;

/// <summary>
/// Input to the Profile Compiler for one compile pass. On the first pass only the natural-language
/// description is set; a revision also carries the <see cref="PreviousDraft"/> and the user's
/// <see cref="RevisionInstruction"/> so the compiler can refine rather than start over
/// (implementation_plan.md, Phase 5).
/// </summary>
public sealed record ProfileCompilationRequest
{
    /// <summary>The plain-language persona description the user authored.</summary>
    public required string NaturalLanguageDescription { get; init; }

    /// <summary>The draft being revised, or <c>null</c> on the first compile.</summary>
    public StructuredProfile? PreviousDraft { get; init; }

    /// <summary>What the user wants changed about <see cref="PreviousDraft"/>, or <c>null</c> on the first compile.</summary>
    public string? RevisionInstruction { get; init; }
}
