using Aimon.Application.Abstractions;

namespace Aimon.Application.Tests;

/// <summary>
/// Guards the Clean Architecture dependency rule for the Application layer: it may depend on Domain
/// but never on Infrastructure, Desktop, or a UI framework.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Application_Does_Not_Depend_On_Outer_Layers()
    {
        var referenced = typeof(IRouter).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .ToArray();

        Assert.DoesNotContain("Aimon.Infrastructure", referenced);
        Assert.DoesNotContain("Aimon.Desktop", referenced);
        Assert.DoesNotContain(referenced, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
