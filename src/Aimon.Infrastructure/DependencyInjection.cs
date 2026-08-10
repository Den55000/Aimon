using Aimon.Application.Abstractions;
using Aimon.Infrastructure.Agents;
using Aimon.Infrastructure.Agents.ClaudeCode;
using Aimon.Infrastructure.Compilation;
using Aimon.Infrastructure.Graphics;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Infrastructure;

/// <summary>Registration entry point for the Infrastructure layer (adapters for Application ports).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure adapters: the on-disk profile store, the DPAPI secret store, the
    /// OpenAI streaming provider, and the OpenAI-backed Profile Compiler (both as typed
    /// <c>HttpClient</c>s). Storage defaults to the current user's application-data folder.
    /// </summary>
    public static IServiceCollection AddAimonInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(ProfileStoreOptions.Default());
        services.AddSingleton<IProfileStore, FileProfileStore>();

        services.AddSingleton(SecretStoreOptions.Default());
        services.AddSingleton<ISecretStore, DpapiSecretStore>();

        services.AddSingleton(OpenAIProviderOptions.Default());
        services.AddHttpClient<IAiProvider, OpenAIProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<OpenAIProviderOptions>();
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton(ProfileCompilerOptions.Default());
        services.AddHttpClient<IProfileCompiler, OpenAIProfileCompiler>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<ProfileCompilerOptions>();
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton<IGraphicsCompiler, FileGraphicsCompiler>();

        // Agentic CLI backends (Phase 9). The process runner is the seam adapters share; Claude Code is
        // the first backend — Codex/Cursor/Copilot register another IAgentBackend the same way.
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton(ClaudeCodeOptions.Default());
        services.AddSingleton<IAgentBackend, ClaudeCodeAgentBackend>();

        return services;
    }
}
