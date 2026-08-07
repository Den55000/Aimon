using Aimon.Application;
using Aimon.Desktop.ViewModels;
using Aimon.Desktop.Views;
using Aimon.Infrastructure;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aimon.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<ShellViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Composition root: the single place that wires Application + Infrastructure + UI together.
    // Later phases register the pipeline, profile store, secret store, and graphics services here.
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddDebug());

        services.AddAimonApplication();
        services.AddAimonInfrastructure();

        services.AddTransient<MainViewModel>();
        services.AddTransient<ProfileCompilerViewModel>();
        services.AddTransient<ShellViewModel>();
    }
}
