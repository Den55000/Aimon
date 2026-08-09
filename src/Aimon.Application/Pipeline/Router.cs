using Aimon.Application.Abstractions;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default <see cref="IRouter"/>: selects the model, enforces hard settings locally, assembles the
/// context messages, and produces an <see cref="ExecutionPlan"/> — or blocks the turn.
/// </summary>
internal sealed class Router : IRouter
{
    public RoutingResult Route(RoutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profile = context.Profile;
        var taskStyle = FindTaskStyle(profile, context.TaskName);

        var model = SelectModel(profile, taskStyle);
        if (model is null)
            return new RoutingResult.Blocked("No model is configured for this profile.");

        var hardSettings = profile.HardSettings;
        if (hardSettings.IsModelBlocked(model.Model))
            return new RoutingResult.Blocked($"Model '{model.Model}' is blocked by this profile's hard settings.");

        var blockedTopic = FindBlockedTopic(hardSettings, context.UserMessage.Content);
        if (blockedTopic is not null)
            return new RoutingResult.Blocked($"This request touches a blocked topic ('{blockedTopic}').");

        var systemPrompt = SystemPromptBuilder.Build(profile.Structured, taskStyle, context.ConversationState);

        var messages = new List<Message>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(Message.System(systemPrompt));
        messages.Add(context.UserMessage);

        var parameters = taskStyle?.ParametersOverride ?? profile.Structured.ModelPreferences.DefaultParameters;

        var plan = new ExecutionPlan
        {
            Model = model,
            Messages = messages,
            Parameters = parameters,
            Graphics = profile.Graphics,
            Mode = context.Mode,
            // In auto mode the chat model decides whether to generate an image by calling the tool;
            // chat/image modes are explicit and don't need the tool offered.
            ImageToolEnabled = context.Mode == TurnMode.Auto,
        };

        return new RoutingResult.Planned(plan);
    }

    private static TaskStyle? FindTaskStyle(Profile profile, string? taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            return null;

        return profile.Structured.TaskStyles
            .FirstOrDefault(style => string.Equals(style.TaskName, taskName, StringComparison.OrdinalIgnoreCase));
    }

    private static ModelRef? SelectModel(Profile profile, TaskStyle? taskStyle)
        => taskStyle?.ModelOverride ?? profile.Structured.ModelPreferences.Preferred.FirstOrDefault();

    private static string? FindBlockedTopic(HardSettings hardSettings, string message)
        => hardSettings.BlockedTopics.FirstOrDefault(topic => message.Contains(topic, StringComparison.OrdinalIgnoreCase));
}
