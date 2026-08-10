using System.Runtime.CompilerServices;
using System.Text;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Agents;
using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default <see cref="IChatService"/>: orchestrates one turn on the selected backend, surfacing the
/// same typed stream to the chat UI and the Graphics Engine.
/// <para>
/// <b>Model provider path</b> (default): Router → Interpreter → provider stream. A hard-settings block
/// short-circuits into an <see cref="AgentState.Error"/> plus a <c>blocked</c> event without contacting
/// the provider; otherwise the completed turn is folded into a compact running
/// <see cref="ConversationState"/> surfaced as a terminal <c>conversation</c> event.
/// </para>
/// <para>
/// <b>CLI agent path</b>: Router still enforces hard settings, then the agent backend streams. The
/// agent's own session carries multi-turn memory, so Aimon's Conversation State Reducer is bypassed
/// (Phase 9 decision A4); the backend surfaces a <c>session</c> event the caller persists to resume.
/// </para>
/// </summary>
internal sealed class ChatService : IChatService
{
    private readonly IRouter _router;
    private readonly IInterpreter _interpreter;
    private readonly IAiProvider _provider;
    private readonly IConversationStateReducer _reducer;
    private readonly IChatBackendRegistry _registry;

    public ChatService(
        IRouter router,
        IInterpreter interpreter,
        IAiProvider provider,
        IConversationStateReducer reducer,
        IChatBackendRegistry registry)
    {
        _router = router;
        _interpreter = interpreter;
        _provider = provider;
        _reducer = reducer;
        _registry = registry;
    }

    public IAsyncEnumerable<ProviderStreamItem> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var backendId = request.Backend ?? _registry.DefaultBackendId;
        return backendId == _registry.DefaultBackendId
            ? RunModelTurnAsync(request, cancellationToken)
            : RunAgentTurnAsync(request, backendId, cancellationToken);
    }

    private async IAsyncEnumerable<ProviderStreamItem> RunModelTurnAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var routing = _router.Route(new RoutingContext
        {
            Profile = request.Profile,
            ConversationState = request.ConversationState,
            UserMessage = request.UserMessage,
            TaskName = request.TaskName,
            Mode = request.Mode,
        });

        if (routing is RoutingResult.Blocked blocked)
        {
            // A blocked turn never reached the provider, so the running state is left unchanged.
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("blocked", blocked.Reason);
            yield break;
        }

        var plan = ((RoutingResult.Planned)routing).Plan;
        var providerRequest = _interpreter.Interpret(plan);

        var assistantText = new StringBuilder();
        var finalState = AgentState.Idle;
        var producedImage = false;

        await foreach (var item in _provider.StreamAsync(providerRequest, cancellationToken).ConfigureAwait(false))
        {
            switch (item)
            {
                case ProviderStreamItem.TextDelta delta:
                    assistantText.Append(delta.Text);
                    break;
                case ProviderStreamItem.ImageArtifact:
                    producedImage = true;
                    break;
                case ProviderStreamItem.StateChange stateChange:
                    finalState = stateChange.State;
                    break;
            }

            yield return item;
        }

        // Close the loop: fold this completed exchange into a compact state for the next turn's Router.
        // Note an image turn textually so the running summary stays coherent when there's no caption.
        var assistantSummary = assistantText.ToString();
        if (assistantSummary.Length == 0 && producedImage)
            assistantSummary = "[Generated an image.]";

        var exchange = new Exchange
        {
            UserMessage = request.UserMessage,
            AssistantMessage = Message.Assistant(assistantSummary),
            FinalState = finalState,
        };

        var nextState = _reducer.Reduce(request.ConversationState, exchange);
        yield return new ProviderStreamItem.ProviderEvent("conversation", nextState);
    }

    private async IAsyncEnumerable<ProviderStreamItem> RunAgentTurnAsync(
        SendMessageRequest request,
        BackendId backendId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_registry.TryGetAgent(backendId, out var agent))
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("error", $"Unknown backend '{backendId}'.");
            yield break;
        }

        if (request.Workspace is null)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("error", $"'{agent.Descriptor.DisplayName}' needs a workspace folder.");
            yield break;
        }

        var routing = _router.RouteAgent(new AgentRoutingContext
        {
            Profile = request.Profile,
            UserMessage = request.UserMessage,
            Workspace = request.Workspace,
            SessionId = request.SessionId,
            PermissionMode = request.PermissionMode,
            TaskName = request.TaskName,
        });

        if (routing is AgentRoutingResult.Blocked blocked)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("blocked", blocked.Reason);
            yield break;
        }

        var agentRequest = ((AgentRoutingResult.Planned)routing).Request;

        // The agent's session carries memory, so no reducer step here — the backend emits terminal
        // state and a `session` event the caller persists for the next turn (A4).
        await foreach (var item in agent.StreamAsync(agentRequest, cancellationToken).ConfigureAwait(false))
            yield return item;
    }
}
