using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;
using Tuner.Audio.Windows;
using Tuner.Core;
using Tuner.UI.Win.ViewModels;

namespace Tuner.UI.Win;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Audio
        services.AddSingleton<IAudioInput, WasapiAudioInput>();

        // Core
        services.AddSingleton<TunerConfiguration>(_ => TunerConfiguration.Default);
        services.AddSingleton<ITunerEngine, TunerEngine>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
