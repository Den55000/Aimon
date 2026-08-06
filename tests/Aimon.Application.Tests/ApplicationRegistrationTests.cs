using Aimon.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class ApplicationRegistrationTests
{
    [Fact]
    public void AddAimonApplication_Is_Chainable_And_Builds_A_Provider()
    {
        var provider = new ServiceCollection()
            .AddAimonApplication()
            .BuildServiceProvider();

        Assert.NotNull(provider);
    }
}
