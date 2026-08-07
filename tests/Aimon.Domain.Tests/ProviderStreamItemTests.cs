using Aimon.Domain;
using Aimon.Domain.Pipeline;

namespace Aimon.Domain.Tests;

public class ProviderStreamItemTests
{
    [Fact]
    public void Variants_Carry_Their_Payloads_And_Are_Matchable()
    {
        var items = new ProviderStreamItem[]
        {
            new ProviderStreamItem.TextDelta("hello"),
            new ProviderStreamItem.StateChange(AgentState.Working),
            new ProviderStreamItem.ProviderEvent("finish", "stop"),
        };

        Assert.True(items[0] is ProviderStreamItem.TextDelta { Text: "hello" });
        Assert.True(items[1] is ProviderStreamItem.StateChange { State: AgentState.Working });
        Assert.True(items[2] is ProviderStreamItem.ProviderEvent { Kind: "finish" });
    }
}
