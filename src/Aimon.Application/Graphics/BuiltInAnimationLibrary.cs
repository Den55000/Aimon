using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Application.Graphics;

/// <summary>
/// A minimal in-memory <see cref="AnimationLibrary"/> covering every <see cref="AgentState"/>, used as
/// the avatar's default until a profile-authored library is loaded. The frame references are logical
/// placeholders (the renderer currently draws a state-coloured stand-in), so the frame-per-state
/// player can be demonstrated without shipping binary assets (implementation_plan.md, D1 "reactions
/// first").
/// </summary>
public static class BuiltInAnimationLibrary
{
    public static AnimationLibrary Create() => new()
    {
        Name = "built-in-default",
        FrameSets =
        [
            new FrameSet { State = AgentState.Idle, Frames = ["idle/0", "idle/1", "idle/2"], FramesPerSecond = 4 },
            new FrameSet { State = AgentState.Listening, Frames = ["listening/0", "listening/1"], FramesPerSecond = 6 },
            new FrameSet { State = AgentState.Working, Frames = ["working/0", "working/1", "working/2", "working/3"], FramesPerSecond = 12 },
            new FrameSet { State = AgentState.Error, Frames = ["error/0", "error/1"], FramesPerSecond = 3 },
        ],
    };
}
