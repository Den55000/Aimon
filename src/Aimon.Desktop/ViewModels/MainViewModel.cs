using Aimon.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aimon.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; }

    // Design-time constructor used by the XAML previewer (Design.DataContext in MainWindow.axaml).
    public MainViewModel()
    {
        Greeting = "Welcome to Aimon!";
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public MainViewModel(IGreetingService greeting)
    {
        Greeting = greeting.GetGreeting();
    }
}
