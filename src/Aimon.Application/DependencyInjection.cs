using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application;

/// <summary>Registration entry point for the Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application-layer use cases: the Router, Interpreter, Conversation State Reducer,
    /// and the ChatService that orchestrates a turn. The provider (<c>IAiProvider</c>) is supplied by
    /// Infrastructure.
    /// </summary>
    public static IServiceCollection AddAimonApplication(this IServiceCollection services)
    {
        services.AddTransient<IRouter, Router>();
        services.AddTransient<IInterpreter, Interpreter>();
        services.AddTransient<IConversationStateReducer, ConversationStateReducer>();
        services.AddTransient<IChatService, ChatService>();
        return services;
    }
}
