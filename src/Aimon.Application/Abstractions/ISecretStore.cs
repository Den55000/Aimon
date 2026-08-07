namespace Aimon.Application.Abstractions;

/// <summary>
/// Stores provider API keys in the OS secret store. Keys are never written into a profile
/// (implementation_plan.md, §3.5 / §3.6).
/// </summary>
public interface ISecretStore
{
    Task SetAsync(string key, string secret, CancellationToken cancellationToken);

    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Task<bool> ContainsAsync(string key, CancellationToken cancellationToken);
}
