using System.Text.Json;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Infrastructure.Persistence;

/// <summary>
/// Serializes profiles to and from the on-disk JSON documents using the source-generated context.
/// Serialization is stable (same profile → same bytes), which the round-trip tests rely on.
/// </summary>
internal static class ProfileSerializer
{
    public static string SerializeProfile(Profile profile)
        => JsonSerializer.Serialize(ProfileMapper.ToDocument(profile), AimonJsonContext.Default.ProfileDocument);

    public static string SerializeHardSettings(HardSettings hardSettings)
        => JsonSerializer.Serialize(ProfileMapper.ToDocument(hardSettings), AimonJsonContext.Default.HardSettingsDocument);

    public static Profile DeserializeProfile(string profileJson, string? hardSettingsJson)
    {
        var document = JsonSerializer.Deserialize(profileJson, AimonJsonContext.Default.ProfileDocument)
            ?? throw new InvalidDataException("Profile document was empty or malformed.");

        var hardSettings = hardSettingsJson is null
            ? HardSettings.None
            : ProfileMapper.ToDomain(
                JsonSerializer.Deserialize(hardSettingsJson, AimonJsonContext.Default.HardSettingsDocument)
                ?? throw new InvalidDataException("Hard-settings document was malformed."));

        return ProfileMapper.ToDomain(document, hardSettings);
    }

    /// <summary>Reads just the summary fields (id, name, timestamp) without materializing a full profile.</summary>
    public static ProfileDocument DeserializeDocument(string profileJson)
        => JsonSerializer.Deserialize(profileJson, AimonJsonContext.Default.ProfileDocument)
           ?? throw new InvalidDataException("Profile document was empty or malformed.");
}
