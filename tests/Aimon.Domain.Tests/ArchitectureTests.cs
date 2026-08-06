using Aimon.Domain;

namespace Aimon.Domain.Tests;

/// <summary>
/// Guards the Clean Architecture dependency rule: the Domain layer must not depend on any
/// outer layer or on a UI framework.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_Has_No_Outward_Project_Dependencies()
    {
        var referenced = typeof(AgentState).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();

        Assert.DoesNotContain("Aimon.Application", referenced);
        Assert.DoesNotContain("Aimon.Infrastructure", referenced);
        Assert.DoesNotContain("Aimon.Desktop", referenced);
        Assert.DoesNotContain(referenced, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
