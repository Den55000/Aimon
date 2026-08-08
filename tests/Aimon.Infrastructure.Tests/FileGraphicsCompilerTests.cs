using Aimon.Domain;
using Aimon.Infrastructure.Graphics;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class FileGraphicsCompilerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static void CreateFrame(TempDirectory dir, string relative)
    {
        var full = Path.Combine(dir.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "frame");
    }

    [Fact]
    public async Task Compiles_By_Convention_From_Per_State_Folders()
    {
        using var dir = new TempDirectory();
        CreateFrame(dir, "idle/0.png");
        CreateFrame(dir, "idle/1.png");
        CreateFrame(dir, "working/0.png");
        CreateFrame(dir, "error/0.png");

        var library = await new FileGraphicsCompiler().CompileAsync(dir.Root, Ct);

        Assert.Equal(3, library.FrameSets.Count);
        Assert.Equal(new[] { "idle/0.png", "idle/1.png" }, library.GetFrameSet(AgentState.Idle)!.Frames);
        Assert.Single(library.GetFrameSet(AgentState.Working)!.Frames);
        Assert.Null(library.GetFrameSet(AgentState.Listening)); // no folder → no frame set
    }

    [Fact]
    public async Task Writes_A_Manifest_When_Compiling_By_Convention()
    {
        using var dir = new TempDirectory();
        CreateFrame(dir, "idle/0.png");

        await new FileGraphicsCompiler().CompileAsync(dir.Root, Ct);

        Assert.True(File.Exists(Path.Combine(dir.Root, "manifest.json")));
    }

    [Fact]
    public async Task Reads_An_Authoritative_Manifest_With_Playback_Overrides()
    {
        using var dir = new TempDirectory();
        CreateFrame(dir, "idle/a.svg");
        CreateFrame(dir, "idle/b.svg");
        File.WriteAllText(Path.Combine(dir.Root, "manifest.json"), """
            {
              "name": "custom",
              "frameSets": [
                { "state": "Idle", "frames": ["idle/a.svg", "idle/b.svg"], "framesPerSecond": 8, "loop": false }
              ]
            }
            """);

        var library = await new FileGraphicsCompiler().CompileAsync(dir.Root, Ct);

        Assert.Equal("custom", library.Name);
        var idle = library.GetFrameSet(AgentState.Idle)!;
        Assert.Equal(8, idle.FramesPerSecond);
        Assert.False(idle.Loop);
    }

    [Fact]
    public async Task Fails_When_A_Referenced_Frame_Is_Missing()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Root, "manifest.json"), """
            { "name": "x", "frameSets": [ { "state": "Idle", "frames": ["idle/missing.svg"] } ] }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() => new FileGraphicsCompiler().CompileAsync(dir.Root, Ct));
    }

    [Fact]
    public async Task Fails_When_There_Are_No_Frames()
    {
        using var dir = new TempDirectory();

        await Assert.ThrowsAsync<InvalidDataException>(() => new FileGraphicsCompiler().CompileAsync(dir.Root, Ct));
    }
}
