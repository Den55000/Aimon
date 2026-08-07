using Aimon.Application.Abstractions;

namespace Aimon.Application.Authoring;

/// <summary>
/// Default <see cref="IProfileDraftSessionFactory"/>: compiles the first draft through the
/// <see cref="IProfileCompiler"/> and returns a session bound to that compiler and the profile store.
/// </summary>
internal sealed class ProfileDraftSessionFactory : IProfileDraftSessionFactory
{
    private readonly IProfileCompiler _compiler;
    private readonly IProfileStore _store;

    public ProfileDraftSessionFactory(IProfileCompiler compiler, IProfileStore store)
    {
        _compiler = compiler;
        _store = store;
    }

    public async Task<ProfileDraftSession> BeginAsync(string name, string naturalLanguageDescription, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalLanguageDescription);

        var firstDraft = await _compiler.CompileAsync(
            new ProfileCompilationRequest { NaturalLanguageDescription = naturalLanguageDescription },
            cancellationToken).ConfigureAwait(false);

        return new ProfileDraftSession(_compiler, _store, name, naturalLanguageDescription, firstDraft);
    }
}
