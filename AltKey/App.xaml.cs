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
        try
        {
            var services = new ServiceCollection();

            services.AddSingleton<ConfigService>();
            services.AddSingleton<LayoutService>();
            services.AddSingleton<InputService>();
            services.AddSingleton<WindowService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<TrayService>();
            services.AddSingleton<ThemeService>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<KeyboardViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // 시스템 테마에 맞춰 초기 테마 적용
            var themeService = Services.GetRequiredService<ThemeService>();
            var config = Services.GetRequiredService<ConfigService>();
            themeService.Apply(config.Current.Theme);

            var window = Services.GetRequiredService<MainWindow>();
            window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "AltKey Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }
}
