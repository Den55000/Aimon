using Aimon.Application.Abstractions;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Infrastructure;

/// <summary>Registration entry point for the Infrastructure layer (adapters for Application ports).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure adapters: the on-disk profile store, the DPAPI secret store, and the
    /// OpenAI streaming provider (as a typed <c>HttpClient</c>). Storage defaults to the current
    /// user's application-data folder.
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

        return services;
    }
}
