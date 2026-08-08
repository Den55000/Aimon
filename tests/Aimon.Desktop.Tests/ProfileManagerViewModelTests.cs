using Aimon.Desktop.Tests.TestSupport;
using Aimon.Desktop.ViewModels;
using Aimon.Domain.Profiles;

namespace Aimon.Desktop.Tests;

public class ProfileManagerViewModelTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Profile MakeProfile(string name) => new()
    {
        Id = ProfileId.New(),
        Name = name,
        Structured = new StructuredProfile { Personality = new Personality { Persona = name } },
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Refresh_Loads_Saved_Profiles_Sorted_By_Name()
    {
        var store = new InMemoryProfileStore();
        await store.SaveAsync(MakeProfile("Beta"), Ct);
        await store.SaveAsync(MakeProfile("Alpha"), Ct);
        var vm = new ProfileManagerViewModel(store);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Alpha", vm.Profiles[0].Name);
        Assert.Equal("Beta", vm.Profiles[1].Name);
    }

    [Fact]
    public async Task Activate_Loads_The_Full_Profile_And_Raises_The_Event()
    {
        var store = new InMemoryProfileStore();
        var saved = MakeProfile("Mentor");
        await store.SaveAsync(saved, Ct);
        var vm = new ProfileManagerViewModel(store);
        await vm.RefreshCommand.ExecuteAsync(null);

        Profile? activated = null;
        vm.ProfileActivated += profile => activated = profile;
        vm.SelectedProfile = vm.Profiles[0];

        await vm.ActivateCommand.ExecuteAsync(null);

        Assert.NotNull(activated);
        Assert.Equal(saved.Id, activated!.Id);
        Assert.Equal("Mentor", activated.Structured.Personality.Persona);
        Assert.Equal("Mentor", vm.ActiveProfileName);
    }

    [Fact]
    public async Task Delete_Removes_The_Profile_And_Refreshes_The_List()
    {
        var store = new InMemoryProfileStore();
        await store.SaveAsync(MakeProfile("Doomed"), Ct);
        var vm = new ProfileManagerViewModel(store);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedProfile = vm.Profiles[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(vm.Profiles);
        Assert.Empty(await store.ListAsync(Ct));
    }

    [Fact]
    public void Activate_And_Delete_Are_Disabled_Without_A_Selection()
    {
        var vm = new ProfileManagerViewModel(new InMemoryProfileStore());

        Assert.Null(vm.SelectedProfile);
        Assert.False(vm.ActivateCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }
}
