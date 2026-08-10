namespace Aimon.Application.Agents;

/// <summary>
/// The result of probing whether a backend can actually run right now — for a CLI agent, whether the
/// executable is installed (and its version). The UI uses this to enable/disable the backend and to
/// show install/login guidance.
/// </summary>
public sealed record BackendAvailability
{
    public required bool IsAvailable { get; init; }

    /// <summary>Detected version string when available (e.g. <c>1.2.3</c>).</summary>
    public string? Version { get; init; }

    /// <summary>Human-readable reason the backend is unavailable, for the UI.</summary>
    public string? Reason { get; init; }

    public static BackendAvailability Available(string? version = null)
        => new() { IsAvailable = true, Version = version };

    public static BackendAvailability Unavailable(string reason)
        => new() { IsAvailable = false, Reason = reason };
}
