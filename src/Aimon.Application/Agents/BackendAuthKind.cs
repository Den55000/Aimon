namespace Aimon.Application.Agents;

/// <summary>How a backend is authenticated, so the UI can guide the user appropriately.</summary>
public enum BackendAuthKind
{
    /// <summary>Aimon supplies an API key from the secret store (model providers).</summary>
    ApiKey,

    /// <summary>The CLI manages its own login/subscription; Aimon just launches it (Phase 9 decision A2).</summary>
    CliNativeLogin,
}
