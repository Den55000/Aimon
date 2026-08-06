using Aimon.Application;
using Aimon.Application.Abstractions;
using Aimon.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Infrastructure.Tests;

public class GreetingServiceTests
{
    [Fact]
    public void Infrastructure_Registers_A_Resolvable_GreetingService()
    {
        var provider = new ServiceCollection()
            .AddAimonApplication()
            .AddAimonInfrastructure()
            .BuildServiceProvider();

        var greeting = provider.GetRequiredService<IGreetingService>();

        Assert.False(string.IsNullOrWhiteSpace(greeting.GetGreeting()));
    }
}
