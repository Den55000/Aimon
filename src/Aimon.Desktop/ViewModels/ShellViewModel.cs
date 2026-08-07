namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Root view model that hosts the app's tabs: the streaming <see cref="Chat"/> and the Profile
/// <see cref="Compiler"/> authoring view. Both are resolved from DI and share the OS secret store.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    public MainViewModel Chat { get; }

    public ProfileCompilerViewModel Compiler { get; }

    // Design-time constructor for the XAML previewer.
    public ShellViewModel()
    {
        Chat = new MainViewModel();
        Compiler = new ProfileCompilerViewModel();
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public ShellViewModel(MainViewModel chat, ProfileCompilerViewModel compiler)
    {
        Chat = chat;
        Compiler = compiler;
    }
}
