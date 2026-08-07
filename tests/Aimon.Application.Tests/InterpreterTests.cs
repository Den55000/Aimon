using Aimon.Application.Abstractions;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class InterpreterTests
{
    [Fact]
    public void Maps_Plan_To_A_Streaming_Provider_Request()
    {
        var interpreter = new ServiceCollection()
            .AddAimonApplication()
            .BuildServiceProvider()
            .GetRequiredService<IInterpreter>();

        var plan = new ExecutionPlan
        {
            Model = new ModelRef("openai", "gpt-4o"),
            Messages = [Message.System("sys"), Message.User("hi")],
            Parameters = new GenerationParameters(temperature: 0.3),
        };

        var request = interpreter.Interpret(plan);

        Assert.True(request.Stream);
        Assert.Equal("gpt-4o", request.Model.Model);
        Assert.Equal(2, request.Messages.Count);
        Assert.Equal(0.3, request.Parameters.Temperature);
    }
}
