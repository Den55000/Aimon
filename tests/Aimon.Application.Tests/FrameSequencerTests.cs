using Aimon.Application.Graphics;
using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Application.Tests;

public class FrameSequencerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // Idle + Working loop; Error is a two-frame one-shot; Listening is intentionally unmapped.
    private static AnimationLibrary Library() => new()
    {
        Name = "test",
        FrameSets =
        [
            new FrameSet { State = AgentState.Idle, Frames = ["idle/a", "idle/b"], FramesPerSecond = 4, Loop = true },
            new FrameSet { State = AgentState.Working, Frames = ["work/a", "work/b", "work/c"], FramesPerSecond = 12, Loop = true },
            new FrameSet { State = AgentState.Error, Frames = ["err/a", "err/b"], FramesPerSecond = 2, Loop = false },
        ],
    };

    private static async Task<FrameSequencer> LoadedEngine()
    {
        var engine = new FrameSequencer();
        await engine.LoadAsync(Library(), Ct);
        return engine;
    }

    [Fact]
    public async Task Selects_The_Frame_Set_For_Each_State()
    {
        var engine = await LoadedEngine();

        engine.TransitionTo(AgentState.Working);
        Assert.Equal(AgentState.Working, engine.CurrentState);
        Assert.Equal("work/a", engine.CurrentFrame);
        Assert.Equal(12, engine.FramesPerSecond);

        engine.TransitionTo(AgentState.Error);
        Assert.Equal("err/a", engine.CurrentFrame);
        Assert.Equal(2, engine.FramesPerSecond);
    }

    [Fact]
    public async Task Falls_Back_To_Idle_Frames_For_An_Unmapped_State()
    {
        var engine = await LoadedEngine();

        engine.TransitionTo(AgentState.Listening);

        Assert.Equal(AgentState.Listening, engine.CurrentState); // the semantic state is still recorded
        Assert.Equal("idle/a", engine.CurrentFrame);             // but frames fall back to Idle
    }

    [Fact]
    public async Task Advance_Loops_A_Looping_Frame_Set()
    {
        var engine = await LoadedEngine();
        engine.TransitionTo(AgentState.Idle);

        Assert.Equal("idle/a", engine.CurrentFrame);
        engine.Advance();
        Assert.Equal("idle/b", engine.CurrentFrame);
        engine.Advance();
        Assert.Equal("idle/a", engine.CurrentFrame); // wrapped back to the start
    }

    [Fact]
    public async Task Advance_Holds_The_Last_Frame_Of_A_One_Shot_Set()
    {
        var engine = await LoadedEngine();
        engine.TransitionTo(AgentState.Error);

        Assert.Equal("err/a", engine.CurrentFrame);
        engine.Advance();
        Assert.Equal("err/b", engine.CurrentFrame);
        engine.Advance();
        Assert.Equal("err/b", engine.CurrentFrame); // no loop: holds on the final frame
    }

    [Fact]
    public async Task Plays_The_Right_Frame_Through_A_Scripted_State_Sequence()
    {
        var engine = await LoadedEngine();

        var frames = new List<string?>();
        foreach (var state in new[] { AgentState.Idle, AgentState.Working, AgentState.Error, AgentState.Idle })
        {
            engine.TransitionTo(state);
            frames.Add(engine.CurrentFrame);
        }

        Assert.Equal(new string?[] { "idle/a", "work/a", "err/a", "idle/a" }, frames);
    }

    [Fact]
    public void Is_Inert_Before_A_Library_Is_Loaded()
    {
        var engine = new FrameSequencer();

        Assert.Null(engine.CurrentFrame);
        Assert.Null(engine.CurrentFrameSet);

        engine.Advance();       // must not throw
        engine.TransitionTo(AgentState.Working);
        Assert.Null(engine.CurrentFrame);
    }

    [Fact]
    public async Task Raises_FrameChanged_On_Transition_And_Advance()
    {
        var engine = await LoadedEngine();
        var changes = 0;
        engine.FrameChanged += (_, _) => changes++;

        engine.TransitionTo(AgentState.Working);
        engine.Advance();

        Assert.Equal(2, changes);
    }
}
