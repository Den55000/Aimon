namespace Aimon.Infrastructure.Compilation;

/// <summary>
/// Configures the OpenAI-backed Profile Compiler: endpoint, secret-store key name, and the model used
/// to compile personas (BYO key path; implementation_plan.md, Phase 5). The model is deliberately
/// configurable so authoring can use a cheaper or stronger model than chat.
/// </summary>
internal sealed class ProfileCompilerOptions
{
    /// <summary>Base address of the OpenAI-compatible API (trailing slash required).</summary>
    public required string BaseAddress { get; init; }

    /// <summary>Name under which the API key is stored in the <c>ISecretStore</c>.</summary>
    public required string SecretKeyName { get; init; }

    /// <summary>The model used to compile the structured profile.</summary>
    public required string Model { get; init; }

    public int TimeoutSeconds { get; init; } = 100;

    public static ProfileCompilerOptions Default() => new()
    {
        BaseAddress = "https://api.openai.com/v1/",
        SecretKeyName = "openai",
        Model = "gpt-5.4-mini",
    };
}
