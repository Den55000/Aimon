using Aimon.Application.Abstractions;
using Aimon.Infrastructure.Greeting;
using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Infrastructure;

/// <summary>Registration entry point for the Infrastructure layer (adapters for Application ports).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure adapters: the on-disk profile store and the DPAPI secret store today;
    /// the OpenAI provider and graphics loaders in later phases. Storage locations default to the
    /// current user's application-data folder.
    /// </summary>
    public static IServiceCollection AddAimonInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();

        services.AddSingleton(ProfileStoreOptions.Default());
        services.AddSingleton<IProfileStore, FileProfileStore>();

        services.AddSingleton(SecretStoreOptions.Default());
        services.AddSingleton<ISecretStore, DpapiSecretStore>();

        return services;
    }
}
