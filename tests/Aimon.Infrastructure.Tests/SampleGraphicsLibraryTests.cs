using Aimon.Domain;
using Aimon.Infrastructure.Graphics;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>Guards the bundled sample animation library in <c>samples/</c> against format drift.</summary>
public class SampleGraphicsLibraryTests
{
    [Fact]
    public async Task Bundled_Sample_Library_Compiles_With_A_Frame_Set_Per_State()
    {
        var repositoryRoot = RepoLayout.FindRepositoryRoot();
        var directory = Path.Combine(repositoryRoot, "samples", "graphics", "default-avatar");

        var library = await new FileGraphicsCompiler().CompileAsync(directory, CancellationToken.None);

        Assert.Equal("default-avatar", library.Name);
        Assert.Equal(4, library.FrameSets.Count);
        foreach (var state in new[] { AgentState.Idle, AgentState.Listening, AgentState.Working, AgentState.Error })
            Assert.NotEmpty(library.GetFrameSet(state)!.Frames);
    }
}
