using System.Runtime.CompilerServices;
using System.Text;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default <see cref="IChatService"/>: orchestrates one turn (Router → Interpreter → provider
/// stream). A hard-settings block short-circuits into an <see cref="AgentState.Error"/> plus a
/// <c>blocked</c> event, without ever contacting the provider. Otherwise the provider's stream —
/// including its lifecycle state changes — is passed straight through, and the completed turn is
/// folded into a compact running <see cref="ConversationState"/> surfaced as a terminal
/// <c>conversation</c> event so the caller can feed it back into the next turn's Router.
/// </summary>
internal sealed class ChatService : IChatService
{
    private readonly IRouter _router;
    private readonly IInterpreter _interpreter;
    private readonly IAiProvider _provider;
    private readonly IConversationStateReducer _reducer;

    public ChatService(IRouter router, IInterpreter interpreter, IAiProvider provider, IConversationStateReducer reducer)
    {
        _router = router;
        _interpreter = interpreter;
        _provider = provider;
        _reducer = reducer;
    }

    public async IAsyncEnumerable<ProviderStreamItem> SendAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
}
