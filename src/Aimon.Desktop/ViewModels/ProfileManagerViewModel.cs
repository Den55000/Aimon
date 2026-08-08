using System.Collections.ObjectModel;
using Aimon.Application.Abstractions;
using Aimon.Application.Profiles;
using Aimon.Domain.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Lists saved profiles and lets the user switch the active one (implementation_plan.md, Phase 7).
/// Activating a profile loads its full form from the store and raises <see cref="ProfileActivated"/>,
/// which the shell wires into the chat and theming. Creating profiles lives in the Profile Compiler
/// tab; this view manages the ones already saved.
/// </summary>
public partial class ProfileManagerViewModel : ViewModelBase
{
    private readonly IProfileStore? _store;

    public ObservableCollection<ProfileSummary> Profiles { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial ProfileSummary? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial string ActiveProfileName { get; set; }

    [ObservableProperty]
    public partial string StatusLabel { get; set; }

    /// <summary>Raised when the user activates a profile; the shell forwards it to chat and theming.</summary>
    public event Action<Profile>? ProfileActivated;

    // Design-time constructor for the XAML previewer.
    public ProfileManagerViewModel()
    {
        ActiveProfileName = "None";
        StatusLabel = "Saved profiles appear here.";
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public ProfileManagerViewModel(IProfileStore store)
        : this()
    {
        _store = store;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_store is null)
            return;

        var summaries = await _store.ListAsync(CancellationToken.None);

        var selectedId = SelectedProfile?.Id;
        Profiles.Clear();
        foreach (var summary in summaries.OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase))
            Profiles.Add(summary);

        // Preserve the selection across a refresh where possible.
        SelectedProfile = Profiles.FirstOrDefault(summary => summary.Id == selectedId);

        StatusLabel = Profiles.Count == 0
            ? "No saved profiles yet — create one in the Profile Compiler tab."
            : $"{Profiles.Count} saved profile(s).";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ActivateAsync()
    {
        if (_store is null || SelectedProfile is null)
            return;

        var profile = await _store.GetAsync(SelectedProfile.Id, CancellationToken.None);
        if (profile is null)
        {
            StatusLabel = "That profile could not be loaded; try refreshing.";
            return;
        }

        ActiveProfileName = profile.Name;
        StatusLabel = $"Activated “{profile.Name}”.";
        ProfileActivated?.Invoke(profile);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (_store is null || SelectedProfile is null)
            return;

        var name = SelectedProfile.Name;
        await _store.DeleteAsync(SelectedProfile.Id, CancellationToken.None);
        await RefreshAsync();
        StatusLabel = $"Deleted “{name}”.";
    }

    private bool HasSelection() => SelectedProfile is not null;
}
