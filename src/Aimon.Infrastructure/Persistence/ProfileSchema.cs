namespace Aimon.Infrastructure.Persistence;

/// <summary>Constants describing the on-disk profile bundle layout (implementation_plan.md, §3.5).</summary>
internal static class ProfileSchema
{
    /// <summary>Current profile schema version. Bump when the on-disk shape changes incompatibly.</summary>
    public const int CurrentVersion = 1;

    /// <summary>File holding the metadata, natural-language source, and structured profile.</summary>
    public const string ProfileFileName = "profile.json";

    /// <summary>Optional file holding safety blocks; absent when there are none.</summary>
    public const string HardSettingsFileName = "hard-settings.json";
}
