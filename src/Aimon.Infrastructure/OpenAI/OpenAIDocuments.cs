using System.Text.Json.Serialization;

namespace Aimon.Infrastructure.OpenAI;

// Wire DTOs for the OpenAI Chat Completions streaming API. Field names are snake_case, set
// explicitly so they are independent of any global naming policy.

internal sealed class OpenAIChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";

    [JsonPropertyName("messages")] public List<OpenAIChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")] public bool Stream { get; set; } = true;

    [JsonPropertyName("temperature")] public double? Temperature { get; set; }

    [JsonPropertyName("top_p")] public double? TopP { get; set; }

    [JsonPropertyName("max_completion_tokens")] public int? MaxCompletionTokens { get; set; }
}

internal sealed class OpenAIChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";

    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

internal sealed class OpenAIStreamChunk
{
    [JsonPropertyName("choices")] public List<OpenAIStreamChoice> Choices { get; set; } = [];
}

internal sealed class OpenAIStreamChoice
{
    [JsonPropertyName("delta")] public OpenAIStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
}

internal sealed class OpenAIStreamDelta
{
    [JsonPropertyName("content")] public string? Content { get; set; }
}
