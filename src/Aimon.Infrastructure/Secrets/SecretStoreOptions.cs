namespace Aimon.Infrastructure.Secrets;

/// <summary>Configures where <see cref="DpapiSecretStore"/> keeps the encrypted secrets file.</summary>
internal sealed class SecretStoreOptions
{
    /// <summary>Path to the JSON file holding DPAPI-protected secrets, keyed by name.</summary>
    public required string FilePath { get; init; }

    /// <summary>Default location under the current user's application-data folder.</summary>
    public static SecretStoreOptions Default() => new()
    {
        FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aimon",
            "secrets.json"),
    };
}
