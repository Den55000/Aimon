using Aimon.Domain;

namespace Aimon.Domain.Tests;

public class AgentStateTests
{
    [Fact]
    public void Default_AgentState_Is_Idle()
    {
        Assert.Equal(AgentState.Idle, default(AgentState));
    }

    [Fact]
    public void AgentState_Contains_The_Canonical_States()
    {
        var states = Enum.GetNames<AgentState>();

        Assert.Contains(nameof(AgentState.Idle), states);
        Assert.Contains(nameof(AgentState.Listening), states);
        Assert.Contains(nameof(AgentState.Working), states);
        Assert.Contains(nameof(AgentState.Error), states);
    }
}
