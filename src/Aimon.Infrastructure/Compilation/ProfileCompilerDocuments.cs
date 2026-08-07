using System.Text.Json.Serialization;

namespace Aimon.Infrastructure.Compilation;

// The JSON shape the compiler model is asked to emit (and that we feed back on revision). It mirrors
// the stylistic fields of a StructuredProfile; model choice is a separate, user-driven concern, so it
// is intentionally not part of the compiled output. Field names are the JSON keys used in the prompt.

internal sealed class ProfileCompilerOutput
{
    [JsonPropertyName("persona")] public string? Persona { get; set; }

    [JsonPropertyName("tone")] public string? Tone { get; set; }

    [JsonPropertyName("traits")] public List<string>? Traits { get; set; }

    [JsonPropertyName("dos")] public List<string>? Dos { get; set; }

    [JsonPropertyName("donts")] public List<string>? Donts { get; set; }

    /// <summary>One of <c>terse</c>, <c>normal</c>, or <c>detailed</c>.</summary>
    [JsonPropertyName("verbosity")] public string? Verbosity { get; set; }

    [JsonPropertyName("formattingPreference")] public string? FormattingPreference { get; set; }

    [JsonPropertyName("taskStyles")] public List<ProfileCompilerTaskStyle>? TaskStyles { get; set; }
}

internal sealed class ProfileCompilerTaskStyle
{
    [JsonPropertyName("taskName")] public string? TaskName { get; set; }

    [JsonPropertyName("systemPromptAddition")] public string? SystemPromptAddition { get; set; }
}

/// <summary>Source-generated JSON context for the compiler's structured output (reflection-free).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProfileCompilerOutput))]
internal sealed partial class ProfileCompilerJsonContext : JsonSerializerContext;
