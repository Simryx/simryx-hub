using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Simryx.App.Services;
using Simryx.App.ViewModels;
using Windows.Graphics;

namespace Simryx.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = default!;

    /// <summary>Иконка в системном трее (создаётся при старте).</summary>
    public static TrayIconService? Tray { get; private set; }

    // Флаг настоящего выхода: чтобы перехватчик закрытия не уводил окно в трей.
    private bool _exiting;

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
        services.AddSingleton<NotificationService>();
        services.AddSingleton<UnitsService>();

        // ViewModels
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Единая идентичность приложения (AUMID): нужна тостам и группировке
        // окон на панели задач. Ставим до создания окна и регистрации тостов.
        try { SetCurrentProcessExplicitAppUserModelID("Simryx.Hub"); } catch { /* не критично */ }
        ShortcutService.EnsureStartMenuShortcut("Simryx.Hub", "Simryx Hub"); // ← добавить
        // Быстрая инициализация языка и темы — до показа окна
        Services.GetRequiredService<ILocalizationService>().Initialize();
        var theme = Services.GetRequiredService<IThemeSelectorService>();
        theme.Initialize();

        // Splash теперь живёт ВНУТРИ MainWindow (оверлей) — никаких отдельных окон и белых полос
        MainWindow = new MainWindow();
        theme.ApplyTheme();

        // «Запускать свёрнутым»: если включено — стартуем сразу в свёрнутом виде
        // без мелькания. Иначе — обычная активация.
        if (ShouldStartMinimized()
            && MainWindow.AppWindow is AppWindow appWindow
            && appWindow.Presenter is OverlappedPresenter presenter)
        {
            StartMinimized(appWindow, presenter);
        }
        else
        {
            MainWindow.Activate();
        }

        // --- Трей и уведомления ---

        // Тосты Windows (часть Windows App SDK, без сторонних пакетов).
        // ВАЖНО: обработчики событий нужно подписать ДО вызова Register().
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        var notifications = Services.GetRequiredService<NotificationService>();
        notifications.Register();

        // Иконка в трее (создаётся всегда, показывается при включённом MinimizeToTray).
        InitializeTray();
        UpdateTrayVisibility();

        // Перехват закрытия окна (крестик) -> сворачивание в трей, если включено.
        if (MainWindow?.AppWindow is AppWindow mainAppWindow)
            mainAppWindow.Closing += OnMainWindowClosing;
    }

    /// <summary>
    /// Запускает окно сразу свёрнутым без визуальной вспышки.
    /// Activate() обязателен (иначе WinUI не инициализирует содержимое и сплэш),
    /// но он показывает окно в обычном виде. Поэтому на время активации уводим
    /// окно за пределы экрана, сворачиваем, а при первом разворачивании
    /// пользователем возвращаем его по центру экрана.
    /// </summary>
    private static void StartMinimized(AppWindow appWindow, OverlappedPresenter presenter)
    {
        // 1. Уводим окно далеко за пределы любого экрана (невидимо).
        appWindow.Move(new PointInt32(-32000, -32000));

        // 2. Активируем — WinUI инициализируется, сплэш стартует, но за кадром.
        MainWindow!.Activate();

        // 3. Сворачиваем в панель задач.
        presenter.Minimize();

        // 4. При первом разворачивании из панели задач — центрируем на экране.
        void OnChanged(AppWindow sender, AppWindowChangedEventArgs eventArgs)
        {
            if (sender.Presenter is OverlappedPresenter p
                && p.State != OverlappedPresenterState.Minimized)
            {
                CenterOnScreen(sender);
                sender.Changed -= OnChanged;
            }
        }

        appWindow.Changed += OnChanged;
    }

    /// <summary>Ставит окно по центру рабочей области текущего экрана.</summary>
    private static void CenterOnScreen(AppWindow appWindow)
    {
        try
        {
            var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var work = display.WorkArea;
            var size = appWindow.Size;
            var x = work.X + (work.Width - size.Width) / 2;
            var y = work.Y + (work.Height - size.Height) / 2;
            appWindow.Move(new PointInt32(x, y));
        }
        catch
        {
            // Если центрирование не удалось — окно просто останется где есть.
        }
    }

    /// <summary>Читает настройку «Запускать свёрнутым» (по умолчанию — выкл).</summary>
    private static bool ShouldStartMinimized()
    {
        try
        {
            return Services?.GetService<ILocalSettingsService>()?.Read<bool?>("StartMinimized") ?? false;
        }
        catch
        {
            return false;
        }
    }

    // --- Трей: инициализация, видимость, закрытие/восстановление ---

    private void InitializeTray()
    {
        if (MainWindow is null) return;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Simryx.ico");
        var en = (Services.GetService<ILocalSettingsService>()?.Read<string>("AppLanguage") ?? "ru-RU")
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);

        Tray = new TrayIconService();
        Tray.OpenRequested += () => MainWindow?.DispatcherQueue.TryEnqueue(ShowMainWindow);
        Tray.ExitRequested += () => MainWindow?.DispatcherQueue.TryEnqueue(ExitApplication);
        Tray.Initialize(
            MainWindow, iconPath, "Simryx Hub",
            en ? "Open Simryx Hub" : "Открыть Simryx Hub",
            en ? "Exit" : "Выход");
    }

    /// <summary>Показывает/прячет иконку трея в зависимости от настройки MinimizeToTray.</summary>
    public static void UpdateTrayVisibility()
    {
        var toTray = Services.GetService<ILocalSettingsService>()?.Read<bool?>("MinimizeToTray") ?? false;
        if (toTray) Tray?.Show();
        else Tray?.Hide();
    }

    private void OnMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting) return; // настоящий выход — не мешаем
        var toTray = Services.GetService<ILocalSettingsService>()?.Read<bool?>("MinimizeToTray") ?? false;
        if (!toTray) return;  // трей выключен — закрытие = выход

        args.Cancel = true;   // не закрываем, прячем в трей
        MainWindow?.AppWindow?.Hide();
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        => MainWindow?.DispatcherQueue.TryEnqueue(ShowMainWindow);

    /// <summary>Показывает и выводит окно на передний план (из трея/тоста).</summary>
    private static void ShowMainWindow()
    {
        if (MainWindow is null) return;
        MainWindow.AppWindow?.Show();
        if (MainWindow.AppWindow?.Presenter is OverlappedPresenter p) p.Restore();
        MainWindow.Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(MainWindow));
    }

    /// <summary>Полный выход из приложения (пункт «Выход» в трее).</summary>
    private void ExitApplication()
    {
        _exiting = true;
        Tray?.Dispose();
        try { Services.GetService<NotificationService>()?.Unregister(); } catch { }
        MainWindow?.Close();
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
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Simryx", "Simryx Hub");
            var logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            // Локальный журнал ведём ВСЕГДА — он нужен для кнопки «Открыть логи»
            // и для диагностики. Эти данные никогда не покидают компьютер,
            // поэтому к настройке приватности отношения не имеют.
            var stamp = DateTimeOffset.Now;
            var line = $"{stamp:O}\t[{source}]\t{ex?.GetType().Name}: {ex?.Message}\n{ex}\n\n";
            File.AppendAllText(Path.Combine(logsDir, "crash.log"), line);

            // Готовый к отправке отчёт о сбое формируем, ТОЛЬКО если пользователь
            // дал согласие (флажок «Отчёты о сбоях» в разделе «Конфиденциальность»).
            // Это будущая очередь на отправку: при появлении сервера отсюда
            // отчёты будут выгружаться и удаляться.
            if (CrashReportsEnabled())
            {
                var reportsDir = Path.Combine(baseDir, "crash-reports");
                Directory.CreateDirectory(reportsDir);
                var fileName = $"crash-{stamp:yyyyMMdd-HHmmss-fff}.txt";
                var report =
                    "Simryx Hub — отчёт о сбое\n" +
                    $"Время: {stamp:O}\n" +
                    $"Источник: {source}\n" +
                    BuildEnvironmentInfo() +
                    "\n--- Исключение ---\n" +
                    $"{ex?.GetType().FullName}: {ex?.Message}\n{ex}\n";
                File.WriteAllText(Path.Combine(reportsDir, fileName), report);
            }
        }
        catch
        {
            // Логирование вылета не должно само ронять приложение.
        }
    }

    /// <summary>Читает согласие пользователя на отчёты о сбоях (по умолчанию — выкл).</summary>
    private static bool CrashReportsEnabled()
    {
        try
        {
            return Services?.GetService<ILocalSettingsService>()?.Read<bool?>("CrashReports") ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Системная информация для отчёта о сбое (без персональных данных).</summary>
    private static string BuildEnvironmentInfo()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var appVersion = asm is null ? "0.1.0" : $"{asm.Major}.{asm.Minor}.{asm.Build}";
            return
                $"Версия приложения: {appVersion}\n" +
                $"ОС: {Environment.OSVersion}\n" +
                $".NET: {Environment.Version}\n" +
                $"Архитектура ОС/процесса: {RuntimeInformation.OSArchitecture} / {RuntimeInformation.ProcessArchitecture}\n" +
                $"Логических процессоров: {Environment.ProcessorCount}\n";
        }
        catch
        {
            return string.Empty;
        }
    }

    // --- Win32 interop ---

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}