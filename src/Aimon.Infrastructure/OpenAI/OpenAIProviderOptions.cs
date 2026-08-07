namespace Aimon.Infrastructure.OpenAI;

/// <summary>Configures the OpenAI adapter: endpoint, secret-store key name, and request timeout.</summary>
internal sealed class OpenAIProviderOptions
{
    /// <summary>Base address of the OpenAI-compatible API (trailing slash required).</summary>
    public required string BaseAddress { get; init; }

    /// <summary>Name under which the API key is stored in the <c>ISecretStore</c>.</summary>
    public required string SecretKeyName { get; init; }

    public int TimeoutSeconds { get; init; } = 100;

    public static OpenAIProviderOptions Default() => new()
    {
        BaseAddress = "https://api.openai.com/v1/",
        SecretKeyName = "openai",
    };
}
