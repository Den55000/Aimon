using Aimon.Application.Abstractions;
using Aimon.Infrastructure.Greeting;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Infrastructure;

/// <summary>Registration entry point for the Infrastructure layer (adapters for Application ports).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure adapters. In later phases this is where the OpenAI provider,
    /// on-disk profile store, secret store, and graphics loaders are wired up.
    /// </summary>
    public static IServiceCollection AddAimonInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
        return services;
    }
}
