using System.Runtime.CompilerServices;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default <see cref="IChatService"/>: orchestrates one turn (Router → Interpreter → provider
/// stream). A hard-settings block short-circuits into an <see cref="AgentState.Error"/> plus a
/// <c>blocked</c> event, without ever contacting the provider. Otherwise the provider's stream —
/// including its lifecycle state changes — is passed straight through.
/// </summary>
internal sealed class ChatService : IChatService
{
    private readonly IRouter _router;
    private readonly IInterpreter _interpreter;
    private readonly IAiProvider _provider;

    public ChatService(IRouter router, IInterpreter interpreter, IAiProvider provider)
    {
        _router = router;
        _interpreter = interpreter;
        _provider = provider;
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
        });

        if (routing is RoutingResult.Blocked blocked)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("blocked", blocked.Reason);
            yield break;
        }

        var plan = ((RoutingResult.Planned)routing).Plan;
        var providerRequest = _interpreter.Interpret(plan);

        await foreach (var item in _provider.StreamAsync(providerRequest, cancellationToken).ConfigureAwait(false))
            yield return item;
    }
}
