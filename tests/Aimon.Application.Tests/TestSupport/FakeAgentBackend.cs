using System.Runtime.CompilerServices;
using Aimon.Application.Abstractions;
using Aimon.Application.Agents;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Tests.TestSupport;

/// <summary>An <see cref="IAgentBackend"/> that replays a scripted sequence and records the last request.</summary>
internal sealed class FakeAgentBackend : IAgentBackend
{
    private readonly IReadOnlyList<ProviderStreamItem> _items;

    public FakeAgentBackend(string id, params ProviderStreamItem[] items)
    {
        Descriptor = new BackendDescriptor
        {
            Id = BackendId.From(id),
            DisplayName = "Claude Code",
            Kind = BackendKind.CliAgent,
            SupportsWorkspace = true,
            SupportsSessions = true,
            AuthKind = BackendAuthKind.CliNativeLogin,
        };
        _items = items;
    }

    public BackendDescriptor Descriptor { get; }

    public int CallCount { get; private set; }

    public AgentTurnRequest? LastRequest { get; private set; }

    public ValueTask<BackendAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(BackendAvailability.Available("test"));

    public async IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        foreach (var item in _items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}
