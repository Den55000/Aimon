using Aimon.Application.Pipeline;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Tests;

public class RoutingResultTests
{
    [Fact]
    public void Planned_Carries_The_Execution_Plan()
    {
        var plan = new ExecutionPlan { Model = new ModelRef("openai", "gpt-4o") };

        RoutingResult result = new RoutingResult.Planned(plan);

        Assert.True(result is RoutingResult.Planned);
        Assert.Same(plan, ((RoutingResult.Planned)result).Plan);
    }

    [Fact]
    public void Blocked_Carries_A_Reason()
    {
        RoutingResult result = new RoutingResult.Blocked("blocked by hard settings");

        Assert.True(result is RoutingResult.Blocked);
        Assert.Equal("blocked by hard settings", ((RoutingResult.Blocked)result).Reason);
    }
}
