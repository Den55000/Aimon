using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Domain.Tests;

public class AnimationLibraryTests
{
    [Fact]
    public void GetFrameSet_Returns_The_Matching_State()
    {
        var library = new AnimationLibrary
        {
            Name = "sample",
            FrameSets =
            [
                new FrameSet { State = AgentState.Idle, Frames = ["idle-01.png"] },
                new FrameSet { State = AgentState.Working, Frames = ["work-01.png", "work-02.png"] },
            ],
        };

        var working = library.GetFrameSet(AgentState.Working);

        Assert.NotNull(working);
        Assert.Equal(2, working.Frames.Count);
    }

    [Fact]
    public void GetFrameSet_Returns_Null_When_State_Is_Not_Defined()
    {
        var library = new AnimationLibrary { Name = "sample" };

        Assert.Null(library.GetFrameSet(AgentState.Error));
    }
}
