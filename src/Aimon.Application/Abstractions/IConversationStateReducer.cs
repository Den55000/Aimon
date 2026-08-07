using Aimon.Domain.Conversations;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Folds the latest <see cref="Exchange"/> into the running <see cref="ConversationState"/>, keeping
/// it compact for the next turn (implementation_plan.md, Phase 4).
/// </summary>
public interface IConversationStateReducer
{
    ConversationState Reduce(ConversationState previous, Exchange latest);
}
