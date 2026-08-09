using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.OpenAI;

/// <summary>
/// <see cref="IAiProvider"/> adapter for OpenAI. It streams Chat Completions (deriving
/// <see cref="AgentState"/> from the request lifecycle), and adds image generation two ways: the
/// chat model calling the <c>generate_image</c> tool in <see cref="TurnMode.Auto"/>, or a direct
/// <c>images/generations</c> call in <see cref="TurnMode.Image"/>. Vision input rides the chat path
/// as multimodal content parts (implementation_plan.md, §3.4).
/// </summary>
internal sealed class OpenAIProvider : IAiProvider
{
    private const string DataPrefix = "data:";
    private const string DoneToken = "[DONE]";
    private const string ImageToolName = "generate_image";
    private const string GeneratedImageMediaType = "image/png";

    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;
    private readonly OpenAIProviderOptions _options;

    public OpenAIProvider(HttpClient httpClient, ISecretStore secretStore, OpenAIProviderOptions options)
    {
        _httpClient = httpClient;
        _secretStore = secretStore;
        _options = options;
    }

    public string Name => "openai";

    public async IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await _secretStore.GetAsync(_options.SecretKeyName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("error", "No OpenAI API key configured.");
            yield break;
        }

        yield return new ProviderStreamItem.StateChange(AgentState.Working);

        // Forced image mode: skip the chat model and generate directly from the user's prompt.
        if (request.Mode == TurnMode.Image)
        {
            var (data, caption, error) = await TryGenerateImageAsync(LastUserText(request.Messages), apiKey, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
            {
                yield return new ProviderStreamItem.StateChange(AgentState.Error);
                yield return new ProviderStreamItem.ProviderEvent("error", error);
                yield break;
            }

            yield return new ProviderStreamItem.ImageArtifact(data!, GeneratedImageMediaType, caption);
            yield return new ProviderStreamItem.StateChange(AgentState.Idle);
            yield break;
        }

        // Chat / auto: stream the chat completion, offering the image tool only in auto mode.
        var (chatResponse, chatError) = await TrySendStreamAsync(
            BuildChatRequest(request, includeTools: request.ImageToolEnabled), apiKey, cancellationToken).ConfigureAwait(false);
        if (chatError is not null)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("error", chatError);
            yield break;
        }

        var toolState = new ToolCallState();
        await foreach (var item in ReadChatStream(chatResponse!, toolState, cancellationToken).ConfigureAwait(false))
            yield return item;

        // The model asked for an image: generate it, then a short follow-up completion for the caption.
        if (request.ImageToolEnabled && toolState.HasImageToolCall)
        {
            var (data, caption, error) = await TryGenerateImageAsync(
                ResolvePrompt(toolState, request.Messages), apiKey, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                yield return new ProviderStreamItem.StateChange(AgentState.Error);
                yield return new ProviderStreamItem.ProviderEvent("error", error);
                yield break;
            }

            yield return new ProviderStreamItem.ImageArtifact(data!, GeneratedImageMediaType, caption);

            var (followUpResponse, followUpError) = await TrySendStreamAsync(
                BuildFollowUpRequest(request, toolState), apiKey, cancellationToken).ConfigureAwait(false);
            if (followUpError is null && followUpResponse is not null)
            {
                await foreach (var item in ReadChatStream(followUpResponse, new ToolCallState(), cancellationToken).ConfigureAwait(false))
                    yield return item;
            }
        }

        yield return new ProviderStreamItem.StateChange(AgentState.Idle);
    }

    // --- Chat streaming ----------------------------------------------------------------------

    private OpenAIChatRequest BuildChatRequest(ProviderRequest request, bool includeTools)
    {
        var body = new OpenAIChatRequest
        {
            Model = request.Model.Model,
            Stream = true,
            Messages = [.. request.Messages.Select(ToRequestMessage)],

            // Send sampling params only when they deviate from the neutral defaults, so reasoning
            // models that accept only the default temperature/top_p still work.
            Temperature = request.Parameters.Temperature == 1.0 ? null : request.Parameters.Temperature,
            TopP = request.Parameters.TopP == 1.0 ? null : request.Parameters.TopP,
            MaxCompletionTokens = request.Parameters.MaxOutputTokens,
        };

        if (includeTools)
        {
            body.Tools = [ImageToolDefinition()];
            body.ToolChoice = "auto";
        }

        return body;
    }

    private OpenAIChatRequest BuildFollowUpRequest(ProviderRequest request, ToolCallState toolState)
    {
        var messages = new List<OpenAIRequestMessage>(request.Messages.Select(ToRequestMessage))
        {
            new()
            {
                Role = "assistant",
                ToolCalls =
                [
                    new OpenAIToolCall
                    {
                        Id = toolState.ToolCallId ?? "",
                        Function = new OpenAIFunctionCall { Name = ImageToolName, Arguments = toolState.Arguments },
                    },
                ],
            },
            new()
            {
                Role = "tool",
                ToolCallId = toolState.ToolCallId ?? "",
                Content = JsonValue.Create("The image was generated and displayed to the user."),
            },
        };

        return new OpenAIChatRequest
        {
            Model = request.Model.Model,
            Stream = true,
            Messages = messages,
            Temperature = request.Parameters.Temperature == 1.0 ? null : request.Parameters.Temperature,
            TopP = request.Parameters.TopP == 1.0 ? null : request.Parameters.TopP,
            MaxCompletionTokens = request.Parameters.MaxOutputTokens,
        };
    }

    private async Task<(HttpResponseMessage? Response, string? Error)> TrySendStreamAsync(
        OpenAIChatRequest body, string apiKey, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body, OpenAIJsonContext.Default.OpenAIChatRequest);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        try
        {
            var response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var message = $"OpenAI request failed with status {(int)response.StatusCode}: {Truncate(errorBody)}";
                response.Dispose();
                return (null, message);
            }

            return (response, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, ex.Message);
        }
        finally
        {
            httpRequest.Dispose();
        }
    }

    private static async IAsyncEnumerable<ProviderStreamItem> ReadChatStream(
        HttpResponseMessage response,
        ToolCallState toolState,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (response)
        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        using (var reader = new StreamReader(stream))
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0 || !line.StartsWith(DataPrefix, StringComparison.Ordinal))
                    continue;

                var payload = line[DataPrefix.Length..].Trim();
                if (payload == DoneToken)
                    break;

                if (!TryParseChunk(payload, out var chunk))
                    continue;

                var choice = chunk.Choices.Count > 0 ? chunk.Choices[0] : null;
                if (choice is null)
                    continue;

                var content = choice.Delta?.Content;
                if (!string.IsNullOrEmpty(content))
                    yield return new ProviderStreamItem.TextDelta(content);

                if (choice.Delta?.ToolCalls is { Count: > 0 } toolCalls)
                    toolState.Accumulate(toolCalls);

                if (!string.IsNullOrEmpty(choice.FinishReason))
                    toolState.FinishReason = choice.FinishReason;
            }
        }

        if (!string.IsNullOrEmpty(toolState.FinishReason))
            yield return new ProviderStreamItem.ProviderEvent("finish", toolState.FinishReason);
    }

    // --- Image generation --------------------------------------------------------------------

    private async Task<(byte[]? Data, string? Caption, string? Error)> TryGenerateImageAsync(
        string prompt, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return (null, null, "No prompt was provided for image generation.");

        var body = new OpenAIImageRequest
        {
            Model = _options.ImageModel,
            Prompt = prompt,
            N = 1,
            Size = _options.ImageSize,
        };

        var json = JsonSerializer.Serialize(body, OpenAIJsonContext.Default.OpenAIImageRequest);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "images/generations")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return (null, null, $"OpenAI image request failed with status {(int)response.StatusCode}: {Truncate(responseBody)}");

            OpenAIImageResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize(responseBody, OpenAIJsonContext.Default.OpenAIImageResponse);
            }
            catch (JsonException)
            {
                return (null, null, "OpenAI image response was malformed.");
            }

            var first = parsed?.Data.Count > 0 ? parsed.Data[0] : null;
            if (string.IsNullOrEmpty(first?.B64Json))
                return (null, null, "OpenAI image response contained no image data.");

            try
            {
                return (Convert.FromBase64String(first.B64Json), first.RevisedPrompt, null);
            }
            catch (FormatException)
            {
                return (null, null, "OpenAI image response was not valid base64.");
            }
        }
        catch (HttpRequestException ex)
        {
            return (null, null, ex.Message);
        }
        finally
        {
            httpRequest.Dispose();
        }
    }

    private static OpenAIToolDefinition ImageToolDefinition() => new()
    {
        Function = new OpenAIFunctionDefinition
        {
            Name = ImageToolName,
            Description =
                "Generate an image from a text description. Call this whenever the user asks to draw, "
                + "create, generate, or produce a picture, image, illustration, logo, or other visual.",
            Parameters = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["prompt"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "A detailed description of the image to generate.",
                    },
                },
                ["required"] = new JsonArray { "prompt" },
            },
        },
    };

    // --- Mapping helpers ---------------------------------------------------------------------

    private static OpenAIRequestMessage ToRequestMessage(Message message)
    {
        var wire = new OpenAIRequestMessage { Role = MapRole(message.Role) };

        if (message.Attachments.Count > 0)
        {
            var parts = new JsonArray();
            if (!string.IsNullOrEmpty(message.Content))
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = message.Content });

            foreach (var attachment in message.Attachments)
            {
                parts.Add(new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = attachment.ToDataUrl() },
                });
            }

            wire.Content = parts;
        }
        else
        {
            wire.Content = JsonValue.Create(message.Content);
        }

        return wire;
    }

    private static string ResolvePrompt(ToolCallState toolState, IReadOnlyList<Message> messages)
    {
        var fromArguments = TryReadPrompt(toolState.Arguments);
        return string.IsNullOrWhiteSpace(fromArguments) ? LastUserText(messages) : fromArguments!;
    }

    private static string? TryReadPrompt(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return null;

        try
        {
            using var document = JsonDocument.Parse(arguments);
            return document.RootElement.TryGetProperty("prompt", out var prompt) ? prompt.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string LastUserText(IReadOnlyList<Message> messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == MessageRole.User)
                return messages[i].Content;
        }

        return "";
    }

    private static bool TryParseChunk(string payload, out OpenAIStreamChunk chunk)
    {
        try
        {
            chunk = JsonSerializer.Deserialize(payload, OpenAIJsonContext.Default.OpenAIStreamChunk) ?? new OpenAIStreamChunk();
            return true;
        }
        catch (JsonException)
        {
            chunk = new OpenAIStreamChunk();
            return false;
        }
    }

    private static string MapRole(MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        MessageRole.Tool => "tool",
        _ => "user",
    };

    private static string Truncate(string value)
        => value.Length <= 500 ? value : value[..500];

    /// <summary>Accumulates the streamed <c>generate_image</c> tool call (index 0) across delta chunks.</summary>
    private sealed class ToolCallState
    {
        private readonly StringBuilder _arguments = new();

        public string? ToolCallId { get; private set; }

        public string? FunctionName { get; private set; }

        public string? FinishReason { get; set; }

        public string Arguments => _arguments.ToString();

        public bool HasImageToolCall =>
            string.Equals(FunctionName, ImageToolName, StringComparison.Ordinal)
            && string.Equals(FinishReason, "tool_calls", StringComparison.Ordinal);

        public void Accumulate(IEnumerable<OpenAIToolCallChunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                // A single tool (generate_image) is supported; accumulate the first tool call only.
                if (chunk.Index != 0)
                    continue;

                if (!string.IsNullOrEmpty(chunk.Id))
                    ToolCallId = chunk.Id;

                if (!string.IsNullOrEmpty(chunk.Function?.Name))
                    FunctionName = chunk.Function!.Name;

                if (!string.IsNullOrEmpty(chunk.Function?.Arguments))
                    _arguments.Append(chunk.Function!.Arguments);
            }
        }
    }
}
