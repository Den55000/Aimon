namespace Aimon.Infrastructure.OpenAI;

/// <summary>Configures the OpenAI adapter: endpoint, secret-store key name, and request timeout.</summary>
internal sealed class OpenAIProviderOptions
{
    /// <summary>Base address of the OpenAI-compatible API (trailing slash required).</summary>
    public required string BaseAddress { get; init; }

    /// <summary>Name under which the API key is stored in the <c>ISecretStore</c>.</summary>
    public required string SecretKeyName { get; init; }

    public int TimeoutSeconds { get; init; } = 100;

    /// <summary>The model used for image generation (the <c>images/generations</c> endpoint).</summary>
    public string ImageModel { get; init; } = "gpt-image-1";

    /// <summary>The size passed to image generation, e.g. <c>1024x1024</c>.</summary>
    public string ImageSize { get; init; } = "1024x1024";

    public static OpenAIProviderOptions Default() => new()
    {
        BaseAddress = "https://api.openai.com/v1/",
        SecretKeyName = "openai",
    };
}
