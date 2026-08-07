using Aimon.Application.Abstractions;
using Aimon.Application.Authoring;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Tests.TestSupport;

/// <summary>
/// An <see cref="IProfileCompiler"/> that returns scripted drafts in order and records the request it
/// was last called with, so the review/revise/confirm state machine can be driven without a model.
/// </summary>
internal sealed class FakeProfileCompiler : IProfileCompiler
{
    private readonly Queue<StructuredProfile> _drafts;

    public FakeProfileCompiler(params StructuredProfile[] drafts) => _drafts = new Queue<StructuredProfile>(drafts);

    public int CallCount { get; private set; }

    public ProfileCompilationRequest? LastRequest { get; private set; }

    public Task<StructuredProfile> CompileAsync(ProfileCompilationRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        // Reuse the final scripted draft once the queue is exhausted, so extra revisions still work.
        var draft = _drafts.Count > 1 ? _drafts.Dequeue() : _drafts.Peek();
        return Task.FromResult(draft);
    }
}
