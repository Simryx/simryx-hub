using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Simryx.App.Services;
using Simryx.App.ViewModels;

namespace Simryx.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = default!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();

        // --- Глобальный перехват вылетов: пишем причину в crash.log ---
        this.UnhandledException += App_OnUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("UnobservedTask", e.Exception);
            e.SetObserved();
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Сервисы
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // ViewModels
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Быстрая инициализация языка и темы — до показа окна
        Services.GetRequiredService<ILocalizationService>().Initialize();
        var theme = Services.GetRequiredService<IThemeSelectorService>();
        theme.Initialize();

        // Splash теперь живёт ВНУТРИ MainWindow (оверлей) — никаких отдельных окон и белых полос
        MainWindow = new MainWindow();
        theme.ApplyTheme();
        MainWindow.Activate();
    }

    /// Реальная фоновая инициализация (вызывается из splash-оверлея MainWindow)
    public static async Task InitializeAsync()
    {
        // Здесь будет реальная подготовка: загрузка профилей, поиск устройств, проверка обновлений.
        // Пока прогреваем сервисы в фоне.
        await Task.Run(() =>
        {
            _ = Services.GetRequiredService<ILocalSettingsService>();
            _ = Services.GetRequiredService<ILocalizationService>();
        });
    }

    // --- Обработчики и запись вылетов ---

    private void App_OnUnhandledException(object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash("UI", e.Exception);
        // e.Handled = true; // раскомментируй, чтобы приложение НЕ закрывалось при ошибке
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Simryx", "Simryx Hub", "logs");
            Directory.CreateDirectory(dir);
            var line = $"{DateTimeOffset.Now:O}\t[{source}]\t{ex?.GetType().Name}: {ex?.Message}\n{ex}\n\n";
            File.AppendAllText(Path.Combine(dir, "crash.log"), line);
        }
        catch
        {
            // Логирование вылета не должно само ронять приложение
        }
    }
}