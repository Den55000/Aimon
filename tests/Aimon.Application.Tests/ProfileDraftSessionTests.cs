using Aimon.Application.Abstractions;
using Aimon.Application.Authoring;
using Aimon.Application.Tests.TestSupport;
using Aimon.Domain.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class ProfileDraftSessionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static (IProfileDraftSessionFactory Factory, FakeProfileCompiler Compiler, InMemoryProfileStore Store) Build(
        params StructuredProfile[] drafts)
    {
        var compiler = new FakeProfileCompiler(drafts);
        var store = new InMemoryProfileStore();
        var services = new ServiceCollection()
            .AddAimonApplication()
            .AddSingleton<IProfileCompiler>(compiler)
            .AddSingleton<IProfileStore>(store)
            .BuildServiceProvider();

        return (services.GetRequiredService<IProfileDraftSessionFactory>(), compiler, store);
    }

    private static StructuredProfile Draft(string persona, Verbosity verbosity = Verbosity.Normal) => new()
    {
        Personality = new Personality { Persona = persona, Tone = "warm", Traits = ["patient"] },
        InteractionRules = new InteractionRules { Verbosity = verbosity, Dos = ["explain the reasoning"] },
    };

    [Fact]
    public async Task Begins_By_Compiling_A_First_Draft()
    {
        var (factory, compiler, _) = Build(Draft("a helpful coding mentor"));

        var session = await factory.BeginAsync("Mentor", "A friendly mentor who teaches coding.", Ct);

        Assert.Equal(1, compiler.CallCount);
        Assert.Equal(ProfileDraftStatus.AwaitingReview, session.Status);
        Assert.Equal(0, session.RevisionCount);
        Assert.True(session.IsValid);
        Assert.Equal("a helpful coding mentor", session.CurrentDraft.Personality.Persona);

        // The first compile carries no previous draft or revision instruction.
        Assert.Null(compiler.LastRequest!.PreviousDraft);
        Assert.Null(compiler.LastRequest.RevisionInstruction);
    }

    [Fact]
    public async Task Revise_Recompiles_From_The_Previous_Draft_And_Instruction()
    {
        var (factory, compiler, _) = Build(Draft("a helpful mentor"), Draft("a terse mentor", Verbosity.Terse));

        var session = await factory.BeginAsync("Mentor", "A mentor.", Ct);
        var firstDraft = session.CurrentDraft;

        await session.ReviseAsync("Make it more concise.", Ct);

        Assert.Equal(2, compiler.CallCount);
        Assert.Equal(1, session.RevisionCount);
        Assert.Equal(Verbosity.Terse, session.CurrentDraft.InteractionRules.Verbosity);
        Assert.Same(firstDraft, compiler.LastRequest!.PreviousDraft);
        Assert.Equal("Make it more concise.", compiler.LastRequest.RevisionInstruction);
    }

    [Fact]
    public async Task Confirm_Persists_Natural_Language_And_Structured_Draft()
    {
        var (factory, _, store) = Build(Draft("a helpful mentor"));

        var session = await factory.BeginAsync("Mentor", "A helpful mentor.", Ct);
        var profile = await session.ConfirmAsync(Ct);

        Assert.Equal(ProfileDraftStatus.Confirmed, session.Status);
        Assert.Equal("Mentor", profile.Name);
        Assert.Equal("A helpful mentor.", profile.NaturalLanguageSource);
        Assert.Equal("a helpful mentor", profile.Structured.Personality.Persona);

        var stored = Assert.Contains(profile.Id, store.Profiles);
        Assert.Equal("A helpful mentor.", stored.NaturalLanguageSource);
        Assert.Equal("a helpful mentor", stored.Structured.Personality.Persona);
    }

    [Fact]
    public async Task Confirm_Is_Rejected_While_The_Draft_Is_Invalid_Then_Succeeds_After_A_Revision()
    {
        // First compile yields an unusable (empty) draft; the revision yields a valid one.
        var (factory, _, store) = Build(StructuredProfile.Empty, Draft("a helpful mentor"));

        var session = await factory.BeginAsync("Mentor", "make me a bot", Ct);

        Assert.False(session.IsValid);
        Assert.NotEmpty(session.ValidationIssues);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConfirmAsync(Ct));
        Assert.Empty(store.Profiles);

        await session.ReviseAsync("Give it a clear persona and tone.", Ct);

        Assert.True(session.IsValid);
        await session.ConfirmAsync(Ct);
        Assert.Single(store.Profiles);
    }

    [Fact]
    public async Task No_Changes_Are_Allowed_After_Confirmation()
    {
        var (factory, _, _) = Build(Draft("a helpful mentor"));

        var session = await factory.BeginAsync("Mentor", "A helpful mentor.", Ct);
        await session.ConfirmAsync(Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ReviseAsync("change it", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConfirmAsync(Ct));
    }
}
