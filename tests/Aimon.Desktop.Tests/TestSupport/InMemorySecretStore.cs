using Aimon.Application.Abstractions;

namespace Aimon.Desktop.Tests.TestSupport;

/// <summary>A minimal in-memory <see cref="ISecretStore"/> for exercising the chat view model.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_secrets.GetValueOrDefault(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _secrets.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_secrets.ContainsKey(key));
}
