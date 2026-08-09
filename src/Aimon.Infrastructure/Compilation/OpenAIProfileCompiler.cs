using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aimon.Application.Abstractions;
using Aimon.Application.Authoring;
using Aimon.Domain.Profiles;
using Aimon.Infrastructure.OpenAI;

namespace Aimon.Infrastructure.Compilation;

/// <summary>
/// <see cref="IProfileCompiler"/> adapter that compiles a natural-language persona into a structured
/// profile with an OpenAI model, requesting a JSON-object response and mapping it through the Domain
/// value objects (so malformed output surfaces as a validation error). The compiler model is
/// configurable and the API key comes from the <c>ISecretStore</c> (BYO key; implementation_plan.md,
/// Phase 5).
/// </summary>
internal sealed class OpenAIProfileCompiler : IProfileCompiler
{
    private const string SystemPrompt =
        """
        You compile a natural-language persona description into a structured assistant profile.
        Respond with a single JSON object and nothing else, using exactly these keys:
          "persona": string — one sentence describing who the assistant is,
          "tone": string — the assistant's tone of voice,
          "traits": array of short trait keywords,
          "dos": array of behavioural do's,
          "donts": array of behavioural don'ts,
          "verbosity": one of "terse", "normal", or "detailed",
          "formattingPreference": string or null — how answers should be formatted,
          "taskStyles": array of objects { "taskName": string, "systemPromptAddition": string } for
          per-task overrides (may be empty).
        Include every key. Infer sensible values from the description; use empty arrays or null where
        the description says nothing.
        """;

    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;
    private readonly ProfileCompilerOptions _options;

    public OpenAIProfileCompiler(HttpClient httpClient, ISecretStore secretStore, ProfileCompilerOptions options)
    {
        _httpClient = httpClient;
        _secretStore = secretStore;
        _options = options;
    }

    public async Task<StructuredProfile> CompileAsync(ProfileCompilationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await _secretStore.GetAsync(_options.SecretKeyName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No OpenAI API key configured; save a key before compiling a profile.");

        using var httpRequest = BuildRequest(request, apiKey);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Profile compilation failed with status {(int)response.StatusCode}: {Truncate(body)}");

        var content = ExtractContent(body);
        var output = ParseOutput(content);
        return ToStructuredProfile(output);
    }

    private HttpRequestMessage BuildRequest(ProfileCompilationRequest request, string apiKey)
    {
        var messages = new List<OpenAIRequestMessage>
        {
            new() { Role = "system", Content = JsonValue.Create(SystemPrompt) },
            new() { Role = "user", Content = JsonValue.Create(BuildUserContent(request)) },
        };

        var body = new OpenAIChatRequest
        {
            Model = _options.Model,
            Stream = false,
            Messages = messages,
            ResponseFormat = new OpenAIResponseFormat(),
        };

        var json = JsonSerializer.Serialize(body, OpenAIJsonContext.Default.OpenAIChatRequest);

        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string BuildUserContent(ProfileCompilationRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Persona description:");
        builder.AppendLine(request.NaturalLanguageDescription.Trim());

        if (request.PreviousDraft is not null && !string.IsNullOrWhiteSpace(request.RevisionInstruction))
        {
            var draftJson = JsonSerializer.Serialize(
                ToOutput(request.PreviousDraft), ProfileCompilerJsonContext.Default.ProfileCompilerOutput);

            builder.AppendLine();
            builder.AppendLine("Current draft:");
            builder.AppendLine(draftJson);
            builder.AppendLine();
            builder.AppendLine("Requested changes:");
            builder.AppendLine(request.RevisionInstruction.Trim());
            builder.AppendLine("Return the full updated JSON object.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractContent(string responseBody)
    {
        OpenAIChatResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(responseBody, OpenAIJsonContext.Default.OpenAIChatResponse);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Profile compiler returned a malformed response.", ex);
        }

        var content = response?.Choices.Count > 0 ? response.Choices[0].Message?.Content : null;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException("Profile compiler returned an empty response.");

        return content;
    }

    private static ProfileCompilerOutput ParseOutput(string content)
    {
        try
        {
            return JsonSerializer.Deserialize(content, ProfileCompilerJsonContext.Default.ProfileCompilerOutput)
                ?? throw new InvalidDataException("Profile compiler returned a null profile object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Profile compiler returned output that was not valid profile JSON.", ex);
        }
    }

    private static StructuredProfile ToStructuredProfile(ProfileCompilerOutput output) => new()
    {
        Personality = new Personality
        {
            Persona = output.Persona?.Trim() ?? "",
            Tone = output.Tone?.Trim() ?? "",
            Traits = CleanList(output.Traits),
        },
        InteractionRules = new InteractionRules
        {
            Dos = CleanList(output.Dos),
            Donts = CleanList(output.Donts),
            Verbosity = ParseVerbosity(output.Verbosity),
            FormattingPreference = string.IsNullOrWhiteSpace(output.FormattingPreference)
                ? null
                : output.FormattingPreference.Trim(),
        },
        TaskStyles = output.TaskStyles is null
            ? []
            : [.. output.TaskStyles
                .Where(style => !string.IsNullOrWhiteSpace(style.TaskName))
                .Select(style => new TaskStyle
                {
                    TaskName = style.TaskName!.Trim(),
                    SystemPromptAddition = string.IsNullOrWhiteSpace(style.SystemPromptAddition)
                        ? null
                        : style.SystemPromptAddition.Trim(),
                })],
    };

    private static ProfileCompilerOutput ToOutput(StructuredProfile draft) => new()
    {
        Persona = draft.Personality.Persona,
        Tone = draft.Personality.Tone,
        Traits = [.. draft.Personality.Traits],
        Dos = [.. draft.InteractionRules.Dos],
        Donts = [.. draft.InteractionRules.Donts],
        Verbosity = draft.InteractionRules.Verbosity.ToString().ToLowerInvariant(),
        FormattingPreference = draft.InteractionRules.FormattingPreference,
        TaskStyles = [.. draft.TaskStyles.Select(style => new ProfileCompilerTaskStyle
        {
            TaskName = style.TaskName,
            SystemPromptAddition = style.SystemPromptAddition,
        })],
    };

    private static List<string> CleanList(List<string>? values)
        => values is null
            ? []
            : [.. values.Select(value => value?.Trim() ?? "").Where(value => value.Length > 0)];

    private static Verbosity ParseVerbosity(string? value)
        => Enum.TryParse<Verbosity>(value, ignoreCase: true, out var verbosity) ? verbosity : Verbosity.Normal;

    private static string Truncate(string value)
        => value.Length <= 500 ? value : value[..500];
}
