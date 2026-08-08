using Aimon.Application.Abstractions;
using Aimon.Application.Authoring;
using Aimon.Application.Graphics;
using Aimon.Application.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application;

/// <summary>Registration entry point for the Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application-layer use cases: the Router, Interpreter, Conversation State Reducer,
    /// the ChatService that orchestrates a turn, and the Profile Compiler authoring session factory.
    /// The provider (<c>IAiProvider</c>), profile store, and compiler are supplied by Infrastructure.
    /// </summary>
    public static IServiceCollection AddAimonApplication(this IServiceCollection services)
    {
        services.AddTransient<IRouter, Router>();
        services.AddTransient<IInterpreter, Interpreter>();
        services.AddTransient<IConversationStateReducer, ConversationStateReducer>();
        services.AddTransient<IChatService, ChatService>();
        services.AddTransient<IProfileDraftSessionFactory, ProfileDraftSessionFactory>();
        services.AddTransient<IGraphicsEngine, FrameSequencer>();
        return services;
    }
}
