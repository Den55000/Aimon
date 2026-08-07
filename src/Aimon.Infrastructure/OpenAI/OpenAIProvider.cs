using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.OpenAI;

/// <summary>
/// <see cref="IAiProvider"/> adapter for OpenAI's streaming Chat Completions API. It derives
/// <see cref="AgentState"/> from the request lifecycle (Working → Idle, or Error), translating the
/// SSE stream into <see cref="ProviderStreamItem"/>s (implementation_plan.md, §3.4).
/// </summary>
internal sealed class OpenAIProvider : IAiProvider
{
    private const string DataPrefix = "data:";
    private const string DoneToken = "[DONE]";

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

        using var httpRequest = BuildRequest(request, apiKey);

        HttpResponseMessage? response = null;
        string? error = null;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                error = $"OpenAI request failed with status {(int)response.StatusCode}: {Truncate(body)}";
            }
        }
        catch (HttpRequestException ex)
        {
            error = ex.Message;
        }

        if (error is not null)
        {
            response?.Dispose();
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("error", error);
            yield break;
        }

        var openResponse = response!;
        using (openResponse)
        await using (var stream = await openResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        using (var reader = new StreamReader(stream))
        {
            string? finishReason = null;
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

                if (!string.IsNullOrEmpty(choice.FinishReason))
                    finishReason = choice.FinishReason;
            }

            if (finishReason is not null)
                yield return new ProviderStreamItem.ProviderEvent("finish", finishReason);
        }

        yield return new ProviderStreamItem.StateChange(AgentState.Idle);
    }

    private HttpRequestMessage BuildRequest(ProviderRequest request, string apiKey)
    {
        var body = new OpenAIChatRequest
        {
            Model = request.Model.Model,
            Stream = true,
            Messages = [.. request.Messages.Select(message => new OpenAIChatMessage
            {
                Role = MapRole(message.Role),
                Content = message.Content,
            })],

            // Send sampling params only when they deviate from the neutral defaults, so reasoning
            // models that accept only the default temperature/top_p still work.
            Temperature = request.Parameters.Temperature == 1.0 ? null : request.Parameters.Temperature,
            TopP = request.Parameters.TopP == 1.0 ? null : request.Parameters.TopP,
            MaxCompletionTokens = request.Parameters.MaxOutputTokens,
        };

        var json = JsonSerializer.Serialize(body, OpenAIJsonContext.Default.OpenAIChatRequest);

        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
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
}
