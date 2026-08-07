using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Aimon.Application.Tests.TestSupport;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Profiles;
using Aimon.Domain.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class RouterTests
{
    private static IRouter CreateRouter()
        => new ServiceCollection().AddAimonApplication().BuildServiceProvider().GetRequiredService<IRouter>();

    private static RoutingContext Context(Profile profile, string userText, string? task = null) => new()
    {
        Profile = profile,
        ConversationState = ConversationState.Empty,
        UserMessage = Message.User(userText),
        TaskName = task,
    };

    [Fact]
    public void Plans_A_Turn_With_System_Then_User_Messages()
    {
        var result = CreateRouter().Route(Context(TestProfiles.WithModel("gpt-4o"), "hello"));

        var planned = Assert.IsType<RoutingResult.Planned>(result);
        Assert.Equal("gpt-4o", planned.Plan.Model.Model);
        Assert.Equal(MessageRole.System, planned.Plan.Messages[0].Role);
        Assert.Equal(MessageRole.User, planned.Plan.Messages[^1].Role);
    }

    [Fact]
    public void Blocks_A_Blocked_Model()
    {
        var profile = TestProfiles.WithModel("gpt-4o", new HardSettings(blockedModels: ["gpt-4o"]));

        Assert.IsType<RoutingResult.Blocked>(CreateRouter().Route(Context(profile, "hello")));
    }

    [Fact]
    public void Blocks_A_Blocked_Topic_Case_Insensitively()
    {
        var profile = TestProfiles.WithModel("gpt-4o", new HardSettings(blockedTopics: ["politics"]));

        var blocked = Assert.IsType<RoutingResult.Blocked>(CreateRouter().Route(Context(profile, "Let's talk about POLITICS today")));
        Assert.Contains("politics", blocked.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Blocks_When_No_Model_Is_Configured()
    {
        Assert.IsType<RoutingResult.Blocked>(CreateRouter().Route(Context(TestProfiles.WithoutModel(), "hello")));
    }

    [Fact]
    public void Uses_The_Task_Style_Model_Override()
    {
        var profile = new Profile
        {
            Id = ProfileId.New(),
            Name = "Test",
            Structured = new StructuredProfile
            {
                ModelPreferences = new ModelPreferences { Preferred = [new ModelRef("openai", "gpt-4o")] },
                TaskStyles = [new TaskStyle { TaskName = "coding", ModelOverride = new ModelRef("openai", "gpt-4o-mini") }],
            },
        };

        var planned = Assert.IsType<RoutingResult.Planned>(CreateRouter().Route(Context(profile, "help me code", task: "coding")));
        Assert.Equal("gpt-4o-mini", planned.Plan.Model.Model);
    }
}
