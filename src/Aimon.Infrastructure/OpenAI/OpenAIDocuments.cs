using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aimon.Infrastructure.OpenAI;

// Wire DTOs for the OpenAI Chat Completions + Images APIs. Field names are snake_case, set
// explicitly so they are independent of any global naming policy.

// ---------------------------------------------------------------------------------------------
// Chat Completions — request
// ---------------------------------------------------------------------------------------------

internal sealed class OpenAIChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";

    [JsonPropertyName("messages")] public List<OpenAIRequestMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")] public bool Stream { get; set; } = true;

    [JsonPropertyName("temperature")] public double? Temperature { get; set; }

    [JsonPropertyName("top_p")] public double? TopP { get; set; }

    [JsonPropertyName("max_completion_tokens")] public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("response_format")] public OpenAIResponseFormat? ResponseFormat { get; set; }

    [JsonPropertyName("tools")] public List<OpenAIToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")] public string? ToolChoice { get; set; }
}

/// <summary>
/// A request-side message. <see cref="Content"/> is a <see cref="JsonNode"/> so it can be either a
/// plain string (text turn) or an array of typed parts (text + image_url) for vision input.
/// </summary>
internal sealed class OpenAIRequestMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";

    [JsonPropertyName("content")] public JsonNode? Content { get; set; }

    [JsonPropertyName("tool_calls")] public List<OpenAIToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")] public string? ToolCallId { get; set; }
}

/// <summary>Constrains the model's output, e.g. <c>{"type":"json_object"}</c> for JSON-only replies.</summary>
internal sealed class OpenAIResponseFormat
{
    [JsonPropertyName("type")] public string Type { get; set; } = "json_object";
}

internal sealed class OpenAIToolDefinition
{
    [JsonPropertyName("type")] public string Type { get; set; } = "function";

    [JsonPropertyName("function")] public OpenAIFunctionDefinition Function { get; set; } = new();
}

internal sealed class OpenAIFunctionDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("description")] public string? Description { get; set; }

    /// <summary>A JSON Schema object describing the function's parameters.</summary>
    [JsonPropertyName("parameters")] public JsonNode? Parameters { get; set; }
}

/// <summary>A completed tool call, replayed to the model in the follow-up request.</summary>
internal sealed class OpenAIToolCall
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("type")] public string Type { get; set; } = "function";

    [JsonPropertyName("function")] public OpenAIFunctionCall Function { get; set; } = new();
}

internal sealed class OpenAIFunctionCall
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("arguments")] public string Arguments { get; set; } = "";
}

// ---------------------------------------------------------------------------------------------
// Chat Completions — non-streaming response (used by the Profile Compiler)
// ---------------------------------------------------------------------------------------------

internal sealed class OpenAIChatResponse
{
    [JsonPropertyName("choices")] public List<OpenAIResponseChoice> Choices { get; set; } = [];
}

internal sealed class OpenAIResponseChoice
{
    [JsonPropertyName("message")] public OpenAIChatMessage? Message { get; set; }
}

/// <summary>A response-side message with plain string content.</summary>
internal sealed class OpenAIChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";

    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

// ---------------------------------------------------------------------------------------------
// Chat Completions — streaming response
// ---------------------------------------------------------------------------------------------

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

    [JsonPropertyName("tool_calls")] public List<OpenAIToolCallChunk>? ToolCalls { get; set; }
}

/// <summary>An incremental slice of a streamed tool call; fragments accumulate across chunks by index.</summary>
internal sealed class OpenAIToolCallChunk
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("function")] public OpenAIFunctionCallChunk? Function { get; set; }
}

internal sealed class OpenAIFunctionCallChunk
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("arguments")] public string? Arguments { get; set; }
}

// ---------------------------------------------------------------------------------------------
// Images — generation
// ---------------------------------------------------------------------------------------------

internal sealed class OpenAIImageRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";

    [JsonPropertyName("n")] public int N { get; set; } = 1;

    [JsonPropertyName("size")] public string? Size { get; set; }
}

internal sealed class OpenAIImageResponse
{
    [JsonPropertyName("data")] public List<OpenAIImageData> Data { get; set; } = [];
}

internal sealed class OpenAIImageData
{
    [JsonPropertyName("b64_json")] public string? B64Json { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("revised_prompt")] public string? RevisedPrompt { get; set; }
}
