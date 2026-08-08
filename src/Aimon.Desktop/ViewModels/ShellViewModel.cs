using Aimon.Domain.Profiles;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Root view model that stitches the app together (implementation_plan.md, Phase 7): the streaming
/// <see cref="Chat"/>, the <see cref="Profiles"/> manager, and the Profile <see cref="Compiler"/>.
/// Activating a profile flows into the chat and re-themes the shell with a per-profile accent; saving
/// a compiled profile refreshes the manager list.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    public MainViewModel Chat { get; }

    public ProfileManagerViewModel Profiles { get; }

    public ProfileCompilerViewModel Compiler { get; }

    [ObservableProperty]
    public partial string ActiveProfileName { get; set; }

    [ObservableProperty]
    public partial IBrush AccentBrush { get; set; }

    // Design-time constructor for the XAML previewer.
    public ShellViewModel()
    {
        Chat = new MainViewModel();
        Profiles = new ProfileManagerViewModel();
        Compiler = new ProfileCompilerViewModel();
        ActiveProfileName = "No profile — using default";
        AccentBrush = AccentFor(null);
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public ShellViewModel(MainViewModel chat, ProfileManagerViewModel profiles, ProfileCompilerViewModel compiler)
    {
        Chat = chat;
        Profiles = profiles;
        Compiler = compiler;
        ActiveProfileName = "No profile — using default";
        AccentBrush = AccentFor(null);

        Profiles.ProfileActivated += OnProfileActivated;
        Compiler.ProfileSaved += OnProfileSaved;

        // Load the saved-profile list at startup (completes quickly against the file store).
        _ = Profiles.RefreshCommand.ExecuteAsync(null);
    }

    private void OnProfileActivated(Profile profile)
    {
        Chat.SetActiveProfile(profile);
        ActiveProfileName = profile.Name;
        AccentBrush = AccentFor(profile.Name);
    }

    private void OnProfileSaved() => _ = Profiles.RefreshCommand.ExecuteAsync(null);

    // A deterministic accent so each profile gets its own recognisable colour.
    private static IBrush AccentFor(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new SolidColorBrush(Color.Parse("#586e75"));

        var hash = 0;
        foreach (var character in name)
            hash = ((hash * 31) + character) & 0x7fffffff;

        return new SolidColorBrush(new HsvColor(1.0, hash % 360, 0.55, 0.80).ToRgb());
    }
}
