using Aimon.Application.Abstractions;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Authoring;

/// <summary>Where a draft sits in the review/confirm loop.</summary>
public enum ProfileDraftStatus
{
    /// <summary>A draft is ready for the user to review, revise, or confirm.</summary>
    AwaitingReview,

    /// <summary>The draft was confirmed and persisted; the session is closed.</summary>
    Confirmed,
}

/// <summary>
/// The review/confirm/revise state machine of the Profile Compiler flow (implementation_plan.md,
/// Phase 5): a first draft is compiled, then the user repeatedly <see cref="ReviseAsync"/>s it (each
/// call recompiles from the previous draft plus an instruction) until they <see cref="ConfirmAsync"/>,
/// which persists the natural-language source and the structured draft together. Created via
/// <see cref="IProfileDraftSessionFactory"/>.
/// </summary>
public sealed class ProfileDraftSession
{
    private readonly IProfileCompiler _compiler;
    private readonly IProfileStore _store;

    internal ProfileDraftSession(
        IProfileCompiler compiler,
        IProfileStore store,
        string name,
        string naturalLanguageDescription,
        StructuredProfile firstDraft)
    {
        _compiler = compiler;
        _store = store;
        Name = name;
        NaturalLanguageDescription = naturalLanguageDescription;
        CurrentDraft = firstDraft;
        ValidationIssues = ProfileDraftValidator.Validate(firstDraft);
    }

    /// <summary>The profile name chosen when the session began.</summary>
    public string Name { get; }

    /// <summary>The plain-language description all drafts are compiled from.</summary>
    public string NaturalLanguageDescription { get; }

    /// <summary>The latest compiled draft awaiting review.</summary>
    public StructuredProfile CurrentDraft { get; private set; }

    /// <summary>Sanity issues with <see cref="CurrentDraft"/>; empty when the draft is confirmable.</summary>
    public IReadOnlyList<string> ValidationIssues { get; private set; }

    /// <summary>How many times the draft has been revised since the first compile.</summary>
    public int RevisionCount { get; private set; }

    public ProfileDraftStatus Status { get; private set; } = ProfileDraftStatus.AwaitingReview;

    /// <summary>True when the current draft has no validation issues and can be confirmed.</summary>
    public bool IsValid => ValidationIssues.Count == 0;

    /// <summary>Recompiles the draft from the original description plus a revision instruction.</summary>
    public async Task ReviseAsync(string revisionInstruction, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionInstruction);
        EnsureAwaitingReview();

        var revised = await _compiler.CompileAsync(
            new ProfileCompilationRequest
            {
                NaturalLanguageDescription = NaturalLanguageDescription,
                PreviousDraft = CurrentDraft,
                RevisionInstruction = revisionInstruction,
            },
            cancellationToken).ConfigureAwait(false);

        CurrentDraft = revised;
        ValidationIssues = ProfileDraftValidator.Validate(revised);
        RevisionCount++;
    }

    /// <summary>
    /// Persists the confirmed profile — natural-language source plus structured draft — and closes the
    /// session. Rejected while the draft has validation issues, so a malformed profile is never stored.
    /// </summary>
    public async Task<Profile> ConfirmAsync(CancellationToken cancellationToken)
    {
        EnsureAwaitingReview();
        if (!IsValid)
            throw new InvalidOperationException("The draft has validation issues and cannot be confirmed; revise it first.");

        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            Id = ProfileId.New(),
            Name = Name,
            NaturalLanguageSource = NaturalLanguageDescription,
            Structured = CurrentDraft,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _store.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        Status = ProfileDraftStatus.Confirmed;
        return profile;
    }

    private void EnsureAwaitingReview()
    {
        if (Status != ProfileDraftStatus.AwaitingReview)
            throw new InvalidOperationException($"The draft session is {Status}; no further changes are allowed.");
    }
}
