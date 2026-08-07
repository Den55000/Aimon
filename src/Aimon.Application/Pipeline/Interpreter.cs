using Aimon.Application.Abstractions;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default <see cref="IInterpreter"/>: turns an <see cref="ExecutionPlan"/> into a provider-agnostic
/// <see cref="ProviderRequest"/>. Kept as a distinct seam so provider-specific shaping has a home.
/// </summary>
internal sealed class Interpreter : IInterpreter
{
    public ProviderRequest Interpret(ExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new ProviderRequest
        {
            Model = plan.Model,
            Messages = plan.Messages,
            Parameters = plan.Parameters,
            Stream = true,
        };
    }
}
