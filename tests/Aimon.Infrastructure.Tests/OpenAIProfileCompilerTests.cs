using System.Net;
using System.Text;
using System.Text.Json;
using Aimon.Application.Authoring;
using Aimon.Domain.Profiles;
using Aimon.Infrastructure.Compilation;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class OpenAIProfileCompilerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static HttpResponseMessage CompletionWith(string profileJson)
    {
        // A chat completion whose message content is the compiler's JSON profile object.
        var body = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = profileJson } } },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private static OpenAIProfileCompiler CreateCompiler(HttpClient httpClient, params (string Key, string Value)[] secrets)
        => new(httpClient, new InMemorySecretStore(secrets), ProfileCompilerOptions.Default());

    [Fact]
    public async Task Compiles_A_Structured_Profile_From_Model_Json()
    {
        const string profileJson = """
            {
              "persona": "a friendly tutor",
              "tone": "encouraging",
              "traits": ["patient", "clear"],
              "dos": ["use examples"],
              "donts": ["be condescending"],
              "verbosity": "detailed",
              "formattingPreference": "use short paragraphs",
              "taskStyles": [{ "taskName": "coding", "systemPromptAddition": "include code samples" }]
            }
            """;

        using var handler = new StubHttpMessageHandler(CompletionWith(profileJson));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var compiler = CreateCompiler(http, ("openai", "sk-test"));

        var draft = await compiler.CompileAsync(
            new ProfileCompilationRequest { NaturalLanguageDescription = "A friendly tutor." }, Ct);

        Assert.Equal("a friendly tutor", draft.Personality.Persona);
        Assert.Equal("encouraging", draft.Personality.Tone);
        Assert.Contains("patient", draft.Personality.Traits);
        Assert.Equal(Verbosity.Detailed, draft.InteractionRules.Verbosity);
        Assert.Contains("use examples", draft.InteractionRules.Dos);
        Assert.Equal("use short paragraphs", draft.InteractionRules.FormattingPreference);
        var taskStyle = Assert.Single(draft.TaskStyles);
        Assert.Equal("coding", taskStyle.TaskName);
    }

    [Fact]
    public async Task Requests_A_Json_Object_From_The_Configured_Model()
    {
        using var handler = new StubHttpMessageHandler(CompletionWith("""{"persona":"a bot"}"""));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var compiler = CreateCompiler(http, ("openai", "sk-test"));

        _ = await compiler.CompileAsync(
            new ProfileCompilationRequest { NaturalLanguageDescription = "A bot." }, Ct);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"model\":\"gpt-5.4-mini\"", handler.LastRequestBody);
        Assert.Contains("\"stream\":false", handler.LastRequestBody);
        Assert.Contains("\"response_format\"", handler.LastRequestBody);
        Assert.Contains("json_object", handler.LastRequestBody);
    }

    [Fact]
    public async Task Sends_The_Previous_Draft_And_Instruction_On_A_Revision()
    {
        using var handler = new StubHttpMessageHandler(CompletionWith("""{"persona":"a concise mentor"}"""));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var compiler = CreateCompiler(http, ("openai", "sk-test"));

        var previous = new StructuredProfile
        {
            Personality = new Personality { Persona = "a verbose mentor" },
        };

        _ = await compiler.CompileAsync(
            new ProfileCompilationRequest
            {
                NaturalLanguageDescription = "A mentor.",
                PreviousDraft = previous,
                RevisionInstruction = "make it more concise",
            }, Ct);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("a verbose mentor", handler.LastRequestBody);
        Assert.Contains("make it more concise", handler.LastRequestBody);
    }

    [Fact]
    public async Task Throws_When_No_Api_Key_Is_Configured()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://stub.local/v1/") };
        var compiler = CreateCompiler(http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => compiler.CompileAsync(
            new ProfileCompilationRequest { NaturalLanguageDescription = "A bot." }, Ct));
    }

    [Fact]
    public async Task Throws_On_A_Non_Success_Status()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"invalid key\"}", Encoding.UTF8, "application/json"),
        };
        using var handler = new StubHttpMessageHandler(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var compiler = CreateCompiler(http, ("openai", "sk-bad"));

        await Assert.ThrowsAsync<HttpRequestException>(() => compiler.CompileAsync(
            new ProfileCompilationRequest { NaturalLanguageDescription = "A bot." }, Ct));
    }
}
