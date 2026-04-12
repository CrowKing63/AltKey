using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ConfigService>();
        services.AddSingleton<LayoutService>();
        services.AddSingleton<InputService>();
        services.AddSingleton<WindowService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<TrayService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<KeyboardViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}