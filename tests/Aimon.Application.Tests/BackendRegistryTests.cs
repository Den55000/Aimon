using Aimon.Application.Abstractions;
using Aimon.Application.Agents;
using Aimon.Application.Tests.TestSupport;
using Aimon.Domain.Agents;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class BackendRegistryTests
{
    private static IChatBackendRegistry Build(params IAgentBackend[] agents)
    {
        var services = new ServiceCollection()
            .AddAimonApplication()
            .AddSingleton<IAiProvider>(new FakeAiProvider());

        foreach (var agent in agents)
            services.AddSingleton(agent);

        return services.BuildServiceProvider().GetRequiredService<IChatBackendRegistry>();
    }

    [Fact]
    public void Default_Backend_Is_The_Model_Provider()
    {
        var registry = Build();

        Assert.Equal(BackendId.From("fake"), registry.DefaultBackendId);
        var descriptor = Assert.Single(registry.Descriptors);
        Assert.Equal(BackendKind.ModelProvider, descriptor.Kind);
    }

    [Fact]
    public void Lists_The_Model_Provider_First_Then_Agents()
    {
        var registry = Build(new FakeAgentBackend("claude-code"));

        Assert.Equal(2, registry.Descriptors.Count);
        Assert.Equal(BackendKind.ModelProvider, registry.Descriptors[0].Kind);
        Assert.Equal(BackendKind.CliAgent, registry.Descriptors[1].Kind);
        Assert.Equal(BackendId.From("claude-code"), registry.Descriptors[1].Id);
    }

    [Fact]
    public void Resolves_An_Agent_By_Id_Case_Insensitively()
    {
        var agent = new FakeAgentBackend("claude-code");
        var registry = Build(agent);

        Assert.True(registry.TryGetAgent(BackendId.From("Claude-Code"), out var resolved));
        Assert.Same(agent, resolved);
    }

    [Fact]
    public void Does_Not_Resolve_The_Model_Provider_As_An_Agent()
    {
        var registry = Build(new FakeAgentBackend("claude-code"));

        Assert.False(registry.TryGetAgent(registry.DefaultBackendId, out _));
    }
}
