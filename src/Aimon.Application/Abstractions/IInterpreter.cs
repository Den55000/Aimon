using Aimon.Domain.Pipeline;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Turns an <see cref="ExecutionPlan"/> into a provider-agnostic <see cref="ProviderRequest"/>
/// (Interpreter; implementation_plan.md, Phase 3).
/// </summary>
public interface IInterpreter
{
    ProviderRequest Interpret(ExecutionPlan plan);
}
