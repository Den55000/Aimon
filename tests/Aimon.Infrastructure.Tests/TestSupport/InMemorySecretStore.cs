using Aimon.Application.Abstractions;

namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>In-memory <see cref="ISecretStore"/> for provider tests (no DPAPI/file IO).</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public InMemorySecretStore(params (string Key, string Value)[] seed)
    {
        foreach (var (key, value) in seed)
            _secrets[key] = value;
    }

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_secrets.TryGetValue(key, out var value) ? value : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _secrets.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_secrets.ContainsKey(key));
}
