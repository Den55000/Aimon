using Aimon.Application.Agents;
using Aimon.Desktop.Tests.TestSupport;
using Aimon.Desktop.ViewModels;

namespace Aimon.Desktop.Tests;

public class MainViewModelBackendTests
{
    private static MainViewModel Build(string? pickedFolder = "/repo")
    {
        var registry = new FakeChatBackendRegistry(FakeChatBackendRegistry.ModelProvider(), new FakeAgentBackend());
        return new MainViewModel(
            new StubChatService(),
            new InMemorySecretStore(),
            new AvatarViewModel(),
            new StubImagePicker(),
            registry,
            new StubWorkspacePicker(pickedFolder));
    }

    [Fact]
    public void Lists_Backends_And_Defaults_To_The_Model_Provider()
    {
        var vm = Build();

        Assert.Equal(2, vm.Backends.Count);
        Assert.Equal(BackendKind.ModelProvider, vm.SelectedBackend.Kind);
        Assert.True(vm.IsModelBackend);
        Assert.False(vm.IsAgentBackend);
        Assert.Equal("No folder selected", vm.WorkspaceDisplay);
    }

    [Fact]
    public void Selecting_A_Cli_Agent_Reveals_The_Workspace_Controls()
    {
        var vm = Build();

        vm.SelectedBackend = vm.Backends[1];

        Assert.True(vm.IsAgentBackend);
        Assert.False(vm.IsModelBackend);
    }

    [Fact]
    public async Task Picking_A_Workspace_Sets_The_Folder()
    {
        var vm = Build(pickedFolder: "/home/denys/project");
        vm.SelectedBackend = vm.Backends[1];

        await vm.PickWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal("/home/denys/project", vm.WorkspacePath);
        Assert.Equal("/home/denys/project", vm.WorkspaceDisplay);
    }
}
