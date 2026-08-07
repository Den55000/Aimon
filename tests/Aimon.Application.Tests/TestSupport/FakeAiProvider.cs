using System.Runtime.CompilerServices;
using Aimon.Application.Abstractions;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Tests.TestSupport;

/// <summary>An <see cref="IAiProvider"/> that replays a scripted sequence and counts invocations.</summary>
internal sealed class FakeAiProvider : IAiProvider
{
    private readonly IReadOnlyList<ProviderStreamItem> _items;

    public FakeAiProvider(params ProviderStreamItem[] items) => _items = items;

    public int CallCount { get; private set; }

    public string Name => "fake";

    public async IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CallCount++;
        foreach (var item in _items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}
