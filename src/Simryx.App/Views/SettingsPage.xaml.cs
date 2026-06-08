using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Simryx.App.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Simryx.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;
    private bool _updateChecking; // Часть 5: защита от наложения проверок
    private readonly ResourceLoader _res = new();
    private readonly ILocalSettingsService _settings = App.Services.GetRequiredService<ILocalSettingsService>();
    private readonly IThemeSelectorService _theme = App.Services.GetRequiredService<IThemeSelectorService>();
    private readonly StartupService _startup = new();

    private static string SettingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Simryx", "Simryx Hub");
    private static string SettingsFile => Path.Combine(SettingsDir, "settings.json");
    private static string LogsDir => Path.Combine(SettingsDir, "logs");

    private bool IsEnglish =>
        (_settings.Read<string>("AppLanguage") ?? "ru-RU").StartsWith("en", StringComparison.OrdinalIgnoreCase);

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Локализованные подписи «Вкл/Выкл» для всех переключателей
        ApplyToggleLabels();

        // Тема
        ThemeCombo.SelectedIndex = _theme.Theme switch
        {
            ElementTheme.Light => 0,
            ElementTheme.Dark => 1,
            _ => 2,
        };

        // Уменьшить анимации
        ReduceMotionToggle.IsOn = _settings.Read<bool?>("ReduceMotion") ?? false;

        // Язык
        var lang = _settings.Read<string>("AppLanguage") ?? "ru-RU";
        LanguageCombo.SelectedIndex = lang == "en-US" ? 1 : 0;

        // Единицы
        var units = _settings.Read<string>("Units") ?? "Metric";
        UnitsCombo.SelectedIndex = units == "Imperial" ? 1 : 0;

        // Запуск
        StartupToggle.IsOn = _startup.IsEnabled();
        StartMinimizedToggle.IsOn = _settings.Read<bool?>("StartMinimized") ?? false;
        TrayToggle.IsOn = _settings.Read<bool?>("MinimizeToTray") ?? false;

        // Обновления
        AutoUpdateToggle.IsOn = _settings.Read<bool?>("AutoCheckUpdates") ?? true;
        var channel = _settings.Read<string>("UpdateChannel") ?? "Stable";
        ChannelCombo.SelectedIndex = channel == "Beta" ? 1 : 0;

        // Уведомления
        NotifyUpdatesToggle.IsOn = _settings.Read<bool?>("NotifyUpdates") ?? true;
        NotifyDevicesToggle.IsOn = _settings.Read<bool?>("NotifyDevices") ?? true;
        NotifyFirmwareToggle.IsOn = _settings.Read<bool?>("NotifyFirmware") ?? true;
        NotifyErrorsToggle.IsOn = _settings.Read<bool?>("NotifyErrors") ?? true;
        TestNotificationBtn.Content = IsEnglish ? "Send test notification" : "Показать тест-уведомление";

        // Конфиденциальность
        StatsToggle.IsOn = _settings.Read<bool?>("AnonymousStats") ?? false;
        CrashToggle.IsOn = _settings.Read<bool?>("CrashReports") ?? false;

        // Версия / лицензия
        // Берём ПОЛНУЮ версию вместе с pre-release-суффиксом (например 0.2.1-beta.1),
        // а не числовую Major.Minor.Build — иначе суффикс «-beta.1» теряется.
        var versionString = GetDisplayVersion();
        VersionText.Text = string.Format(_res.GetString("VersionFormat"), versionString);
        VersionBadge.Text = string.Format(_res.GetString("VersionFormat"), versionString);
        LicenseText.Text = _res.GetString("LicenseInactive");

        _loaded = true;
    }

    /// <summary>
    /// Отображаемая версия приложения вместе с pre-release-суффиксом (например "0.2.1-beta.1").
    /// Источник — AssemblyInformationalVersion (туда из &lt;Version&gt; попадает полный semver).
    /// Отрезаем "+&lt;git-hash&gt;", который .NET SDK добавляет к информационной версии.
    /// </summary>
    private static string GetDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        var v = asm.GetName().Version;
        return v is null ? "0.1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // Единые локализованные подписи для всех ToggleSwitch (не зависят от языка ОС)
    private void ApplyToggleLabels()
    {
        var on = _res.GetString("ToggleOn");
        var off = _res.GetString("ToggleOff");
        var toggles = new[]
        {
            ReduceMotionToggle, StartupToggle, StartMinimizedToggle, TrayToggle,
            AutoUpdateToggle, NotifyUpdatesToggle, NotifyDevicesToggle,
            NotifyFirmwareToggle, NotifyErrorsToggle, StatsToggle, CrashToggle,
        };
        foreach (var t in toggles)
        {
            t.OnContent = on;
            t.OffContent = off;
        }
    }

    // ===== Внешний вид =====
    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var theme = ThemeCombo.SelectedIndex switch
        {
            0 => ElementTheme.Light,
            1 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        _theme.SetTheme(theme);
    }

    private void ReduceMotion_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _settings.Save("ReduceMotion", ReduceMotionToggle.IsOn);
        MotionService.Reduced = ReduceMotionToggle.IsOn; // вживую, без перезапуска
    }

    // ===== Язык и регион =====
    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var lang = LanguageCombo.SelectedIndex == 1 ? "en-US" : "ru-RU";
        _settings.Save("AppLanguage", lang);
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
        RestartInfoBar.IsOpen = true;
    }

    private void Units_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        _settings.Save("Units", UnitsCombo.SelectedIndex == 1 ? "Imperial" : "Metric");
        App.Services.GetService<UnitsService>()?.NotifyChanged(); // живое обновление открытых экранов
    }

    // ===== Запуск и система =====
    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        try
        {
            _startup.SetEnabled(StartupToggle.IsOn);
        }
        catch
        {
            ShowStatus(_res.GetString("StatusStartupFailed"));
            StartupToggle.IsOn = _startup.IsEnabled();
        }
    }

    private void Tray_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _settings.Save("MinimizeToTray", TrayToggle.IsOn);
        App.UpdateTrayVisibility(); // мгновенно показываем/прячем иконку трея
    }

    // Общий обработчик для простых переключателей (ключ — в Tag)
    private void PersistToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        if (sender is ToggleSwitch ts && ts.Tag is string key)
        {
            _settings.Save(key, ts.IsOn);
        }
    }

    // Тест-уведомления: шлём по одному тосту на каждую категорию.
    // Выключенные в настройках категории молчат — так проверяется и гейтинг тумблеров.
    private void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        var notifications = App.Services.GetService<NotificationService>();
        if (notifications is null) return;

        var en = IsEnglish;
        notifications.NotifyUpdate(
            en ? "Update available" : "Доступно обновление",
            en ? "Test of update notifications." : "Проверка уведомлений об обновлениях.");
        notifications.NotifyDevice(
            en ? "Device connected" : "Устройство подключено",
            en ? "Test of device notifications." : "Проверка уведомлений об устройствах.");
        notifications.NotifyFirmware(
            en ? "Firmware update" : "Обновление прошивки",
            en ? "Test of firmware notifications." : "Проверка уведомлений о прошивке.");
        notifications.NotifyError(
            en ? "Error example" : "Пример ошибки",
            en ? "Test of error notifications." : "Проверка уведомлений об ошибках.");

        ShowStatus(InfoBarSeverity.Informational,
            string.Empty,
            en ? "Test notifications sent (only enabled categories appear)."
               : "Тест-уведомления отправлены (придут только включённые категории).");
    }

    // ===== Обновления (Часть 2 + 3 + 5) =====
    private async void Channel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        // Часть 5: сохраняем выбранный канал — единый источник правды для всех проверок.
        _settings.Save("UpdateChannel", ChannelCombo.SelectedIndex == 1 ? "Beta" : "Stable");

        // Канал сменился: сбрасываем устаревший кэш тихой авто-проверки,
        // чтобы на Главной не висел баннер из прошлого канала, и сразу
        // ТИХО перепроверяем в новом канале — результат в строке статуса,
        // без модального окна (его показываем только по явной кнопке).
        UpdateCoordinator.ResetSession();
        await RunUpdateCheckAsync(silent: true);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        => await RunUpdateCheckAsync(silent: false);

    // Текущий канал из настройки (единый разбор).
    private UpdateChannel ReadChannel() =>
        (_settings.Read<string>("UpdateChannel") ?? "Stable")
        .Equals("Beta", StringComparison.OrdinalIgnoreCase)
        ? UpdateChannel.Beta
        : UpdateChannel.Stable;

    // Общая проверка обновлений.
    // silent = true: смена канала — результат в строке статуса, без модалки.
    // silent = false: кнопка «Проверить» — при наличии обновления показываем диалог.
    private async Task RunUpdateCheckAsync(bool silent = false)
    {
        if (_updateChecking) return; // не запускаем параллельные проверки/диалоги
        _updateChecking = true;

        var en = IsEnglish;
        CheckUpdatesBtn.IsEnabled = false;
        ShowStatus(InfoBarSeverity.Informational,
            string.Empty,
            en ? "Checking for updates…" : "Проверяем обновления…");

        try
        {
            var result = await new UpdateService().CheckForUpdatesAsync(ReadChannel());
            await ApplyUpdateResultAsync(result, en, silent);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error,
                en ? "Update check failed" : "Ошибка проверки обновлений",
                ex.Message);
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            _updateChecking = false;
        }
    }

    private async Task ApplyUpdateResultAsync(UpdateCheckResult result, bool en, bool silent)
    {
        switch (result.Status)
        {
            case UpdateStatus.UpdateAvailable when result.Info is not null:
            {
                var info = result.Info;

                // Тихий режим (смена канала): не дёргаем модалку, показываем статус.
                if (silent)
                {
                    ShowStatus(InfoBarSeverity.Informational,
                        en ? $"Version {info.Version} is available"
                           : $"Доступна версия {info.Version}",
                        en ? "Click \"Check for updates\" to install."
                           : "Нажмите «Проверить обновления», чтобы установить.");
                    break;
                }

                StatusInfoBar.IsOpen = false;
                var hasInstaller = !string.IsNullOrWhiteSpace(info.DownloadUrl);
                var dialog = new ContentDialog
                {
                    Title = en ? $"Version {info.Version} is available"
                        : $"Доступна версия {info.Version}",
                    Content = BuildNotesContent(info, en),
                    CloseButtonText = en ? "Later" : "Позже",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                    RequestedTheme = ActualTheme,
                };
                if (hasInstaller)
                {
                    dialog.PrimaryButtonText = en ? "Update now" : "Обновить сейчас";
                    dialog.SecondaryButtonText = en ? "Release page" : "Страница релиза";
                }
                else
                {
                    dialog.PrimaryButtonText = en ? "Open release page" : "Открыть страницу релиза";
                }

                var choice = await dialog.ShowAsync();
                if (hasInstaller)
                {
                    if (choice == ContentDialogResult.Primary)
                        await UpdateFlow.RunAsync(info, XamlRoot, en, ActualTheme);
                    else if (choice == ContentDialogResult.Secondary && !string.IsNullOrWhiteSpace(info.ReleaseUrl))
                        await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
                }
                else if (choice == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(info.ReleaseUrl))
                {
                    await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
                }
                break;
            }

            case UpdateStatus.RollbackAvailable when result.Info is not null:
            {
                // Установлена бета-сборка, но выбран стабильный канал, а последняя
                // стабильная версия не выше текущей → предлагаем вернуться на стабильную.
                var info = result.Info;
                var currentDisplay = GetDisplayVersion();

                // Тихий режим (смена канала): без модалки, только строка статуса.
                if (silent)
                {
                    ShowStatus(InfoBarSeverity.Informational,
                        en ? $"Stable version {info.Version} is available"
                           : $"Доступна стабильная версия {info.Version}",
                        en ? "You're on a beta build. Click \"Check for updates\" to switch to stable."
                           : "У вас бета-сборка. Нажмите «Проверить обновления», чтобы вернуться на стабильную.");
                    break;
                }

                StatusInfoBar.IsOpen = false;
                var hasInstaller = !string.IsNullOrWhiteSpace(info.DownloadUrl);
                var intro = en
                    ? $"You're using a beta build ({currentDisplay}). You can roll back to the stable version {info.Version}."
                    : $"Вы используете бета-сборку ({currentDisplay}). Можно вернуться на стабильную версию {info.Version}.";

                var dialog = new ContentDialog
                {
                    Title = en ? $"Roll back to stable {info.Version}?"
                               : $"Вернуться на стабильную {info.Version}?",
                    Content = BuildRollbackContent(intro, info, en),
                    CloseButtonText = en ? "Later" : "Позже",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                    RequestedTheme = ActualTheme,
                };
                if (hasInstaller)
                {
                    dialog.PrimaryButtonText = en ? "Install stable" : "Установить стабильную";
                    dialog.SecondaryButtonText = en ? "Release page" : "Страница релиза";
                }
                else
                {
                    dialog.PrimaryButtonText = en ? "Open release page" : "Открыть страницу релиза";
                }

                var choice = await dialog.ShowAsync();
                if (hasInstaller)
                {
                    if (choice == ContentDialogResult.Primary)
                        await UpdateFlow.RunAsync(info, XamlRoot, en, ActualTheme);
                    else if (choice == ContentDialogResult.Secondary && !string.IsNullOrWhiteSpace(info.ReleaseUrl))
                        await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
                }
                else if (choice == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(info.ReleaseUrl))
                {
                    await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
                }
                break;
            }

            case UpdateStatus.Failed:
                ShowStatus(InfoBarSeverity.Error,
                    en ? "Update check failed" : "Ошибка проверки обновлений",
                    result.Error ?? string.Empty);
                break;

            default:
                ShowStatus(InfoBarSeverity.Success,
                    en ? "You're up to date" : "Установлена последняя версия",
                    string.Empty);
                break;
        }
    }

    private static UIElement BuildNotesContent(UpdateInfo info, bool en)
    {
        var text = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? (en ? "No release notes." : "Заметки к релизу отсутствуют.")
                : info.ReleaseNotes,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        return new ScrollViewer
        {
            MaxHeight = 360,
            Content = text,
        };
    }

    // Контент диалога отката: пояснение сверху + заметки к стабильному релизу.
    private static UIElement BuildRollbackContent(string intro, UpdateInfo info, bool en)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = intro,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(BuildNotesContent(info, en));
        return panel;
    }

    // ===== Дополнительно =====
    private async void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LogsDir);
        var folder = await StorageFolder.GetFolderFromPathAsync(LogsDir);
        await Launcher.LaunchFolderAsync(folder);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(SettingsFile))
        {
            ShowStatus(_res.GetString("StatusExportNoFile"));
            return;
        }
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "simryx-settings",
        };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        InitializeWithWindow(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await FileIO.WriteTextAsync(file, File.ReadAllText(SettingsFile));
        ShowStatus(_res.GetString("StatusExported"));
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsFile, await FileIO.ReadTextAsync(file));
        // Файл заменён в обход сервиса — обновляем кэш, иначе следующий Save
        // перезапишет импортированные настройки устаревшими значениями из памяти.
        _settings.Reload();
        ShowStatus(_res.GetString("StatusImported"));
        RestartInfoBar.IsOpen = true;
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = _res.GetString("ResetTitle"),
            Content = _res.GetString("ResetBody"),
            PrimaryButtonText = _res.GetString("ResetPrimary"),
            CloseButtonText = _res.GetString("ResetClose"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (File.Exists(SettingsFile)) File.Delete(SettingsFile);
        Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
    }

    // ===== О программе =====
    private async void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is string url)
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
    }

    // ===== Вспомогательное =====
    private void Restart_Click(object sender, RoutedEventArgs e)
        => Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);

    private void ShowStatus(string message)
        => ShowStatus(InfoBarSeverity.Informational, string.Empty, message);

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private static void InitializeWithWindow(object target)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(target, hwnd);
    }
}