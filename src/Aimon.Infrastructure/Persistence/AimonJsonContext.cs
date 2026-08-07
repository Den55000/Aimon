using System.Text.Json.Serialization;

namespace Aimon.Infrastructure.Persistence;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for all persisted documents. Using the
/// generator keeps serialization reflection-free (trim/AOT-friendly) and centralizes the on-disk
/// conventions: camelCase names, string enums, indented output, and nulls omitted.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProfileDocument))]
[JsonSerializable(typeof(HardSettingsDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AimonJsonContext : JsonSerializerContext;
