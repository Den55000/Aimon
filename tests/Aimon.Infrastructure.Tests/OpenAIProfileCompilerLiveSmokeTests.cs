using Aimon.Application.Authoring;
using Aimon.Infrastructure.Compilation;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>
/// Live smoke test of the Profile Compiler against the real OpenAI API. Gated on
/// <c>AIMON_OPENAI_API_KEY</c>, so it is a no-op in CI and by default; set the key (and optionally
/// <c>AIMON_OPENAI_MODEL</c>) to exercise natural-language → structured compilation.
/// </summary>
[Trait("Category", "Live")]
public class OpenAIProfileCompilerLiveSmokeTests
{
    [Fact]
    public async Task Live_Compiles_A_Structured_Profile_From_Prose_When_A_Key_Is_Provided()
    {
        var apiKey = Environment.GetEnvironmentVariable("AIMON_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return; // Gated: no key in the environment (e.g. CI), so skip the live call.

        var model = Environment.GetEnvironmentVariable("AIMON_OPENAI_MODEL") ?? "gpt-5.4-mini";

        using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new ProfileCompilerOptions
        {
            BaseAddress = "https://api.openai.com/v1/",
            SecretKeyName = "openai",
            Model = model,
        };
        var compiler = new OpenAIProfileCompiler(http, new InMemorySecretStore(("openai", apiKey)), options);

        var draft = await compiler.CompileAsync(
            new ProfileCompilationRequest
            {
                NaturalLanguageDescription =
                    "A warm, patient coding mentor who explains concepts with small examples and never condescends.",
            },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(draft.Personality.Persona));
    }
}
