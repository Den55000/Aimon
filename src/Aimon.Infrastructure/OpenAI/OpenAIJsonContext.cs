using System.Text.Json.Serialization;

namespace Aimon.Infrastructure.OpenAI;

/// <summary>Source-generated JSON context for the OpenAI wire DTOs (reflection-free serialization).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenAIChatRequest))]
[JsonSerializable(typeof(OpenAIStreamChunk))]
[JsonSerializable(typeof(OpenAIChatResponse))]
internal sealed partial class OpenAIJsonContext : JsonSerializerContext;
