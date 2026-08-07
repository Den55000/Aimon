using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aimon.Application.Abstractions;
using Aimon.Infrastructure.Persistence;

namespace Aimon.Infrastructure.Secrets;

/// <summary>
/// Stores secrets (API keys) encrypted with Windows DPAPI, scoped to the current user
/// (implementation_plan.md, §3.6). Values are protected individually and kept in a JSON map so the
/// file on disk never contains plaintext. DPAPI is Windows-only; other platforms get an
/// encrypted-file or keychain adapter behind the same <see cref="ISecretStore"/> port later.
/// </summary>
internal sealed class DpapiSecretStore : ISecretStore
{
    // Application-specific entropy mixed into every DPAPI blob.
    private static readonly byte[] Entropy = "Aimon.Secrets.v1"u8.ToArray();

    private readonly SecretStoreOptions _options;

    public DpapiSecretStore(SecretStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secret);

        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        entries[key] = Protect(secret);
        await SaveAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return entries.TryGetValue(key, out var protectedValue) ? Unprotect(protectedValue) : null;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (entries.Remove(key))
            await SaveAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ContainsAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return entries.ContainsKey(key);
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var json = await File.ReadAllTextAsync(_options.FilePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var entries = JsonSerializer.Deserialize(json, AimonJsonContext.Default.DictionaryStringString);
        return entries is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(entries, StringComparer.Ordinal);
    }

    private async Task SaveAsync(Dictionary<string, string> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(entries, AimonJsonContext.Default.DictionaryStringString);
        await File.WriteAllTextAsync(_options.FilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private static string Protect(string plaintext)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI secret storage requires Windows.");

        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string protectedValue)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI secret storage requires Windows.");

        var plaintextBytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
