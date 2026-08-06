using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application;

/// <summary>Registration entry point for the Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer use cases into the DI container. Nothing concrete is
    /// registered yet; the Router, Interpreter, Conversation State Reducer, and Profile
    /// Compiler orchestration are added in later phases.
    /// </summary>
    public static IServiceCollection AddAimonApplication(this IServiceCollection services)
    {
        return services;
    }
}
