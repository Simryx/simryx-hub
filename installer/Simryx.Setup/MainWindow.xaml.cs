using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Simryx.Setup.Services;

namespace Simryx.Setup;

public partial class MainWindow : Window
{
    private enum Step { Welcome, License, Location, Options, Progress, Finish, Uninstall }

    private readonly bool _uninstall;
    private readonly List<Step> _sequence;
    private int _index;
    private bool _busy;
    private bool _done;
    private readonly InstallService _service = new();
    private InstallResult? _result;
    private string _uninstallDir = "";

    public MainWindow(bool uninstall)
    {
        InitializeComponent();
        _uninstall = uninstall;
        LicenseBox.Text = AppInfo.License;
        PathBox.Text = AppInfo.DefaultInstallDir;
        _sequence = uninstall
            ? new List<Step> { Step.Uninstall, Step.Progress, Step.Finish }
            : new List<Step> { Step.Welcome, Step.License, Step.Location, Step.Options, Step.Progress, Step.Finish };

        if (uninstall && !InstallService.IsInstalled())
        {
            UninstallText.Text = "Установка Simryx Hub не найдена в системе.";
            _uninstallDir = InstallService.ReadInstallLocation();
            PurgeData.Visibility = Visibility.Collapsed;
            PurgeHint.Visibility = Visibility.Collapsed;
        }
        else if (uninstall)
        {
            _uninstallDir = InstallService.ReadInstallLocation();
            UninstallText.Text =
                $"Приложение в папке\n{_uninstallDir}\nи все его ярлыки будут удалены. Продолжить?";
        }

        ShowCurrent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.UseDarkTitleBar(hwnd, !ThemeManager.IsLight);
        Native.TryEnableMica(hwnd);
    }

    private Step Current => _sequence[_index];

    private void ShowCurrent()
    {
        PanelWelcome.Visibility = Visibility.Collapsed;
        PanelLicense.Visibility = Visibility.Collapsed;
        PanelLocation.Visibility = Visibility.Collapsed;
        PanelOptions.Visibility = Visibility.Collapsed;
        PanelProgress.Visibility = Visibility.Collapsed;
        PanelFinish.Visibility = Visibility.Collapsed;
        PanelUninstall.Visibility = Visibility.Collapsed;

        switch (Current)
        {
            case Step.Welcome: PanelWelcome.Visibility = Visibility.Visible; StepTitle.Text = "Добро пожаловать"; break;
            case Step.License: PanelLicense.Visibility = Visibility.Visible; StepTitle.Text = "Шаг 2 из 4 · Лицензия"; break;
            case Step.Location: PanelLocation.Visibility = Visibility.Visible; StepTitle.Text = "Шаг 3 из 4 · Папка установки"; break;
            case Step.Options: PanelOptions.Visibility = Visibility.Visible; StepTitle.Text = "Шаг 4 из 4 · Параметры"; break;
            case Step.Progress: PanelProgress.Visibility = Visibility.Visible; StepTitle.Text = _uninstall ? "Удаление" : "Установка"; break;
            case Step.Finish: PanelFinish.Visibility = Visibility.Visible; StepTitle.Text = "Завершение"; break;
            case Step.Uninstall: PanelUninstall.Visibility = Visibility.Visible; StepTitle.Text = "Удаление"; break;
        }

        UpdateNav();
    }

    private void UpdateNav()
    {
        BtnBack.Visibility = (_index > 0 && Current != Step.Progress && Current != Step.Finish)
            ? Visibility.Visible : Visibility.Hidden;
        BtnCancel.Visibility = (Current == Step.Progress || Current == Step.Finish)
            ? Visibility.Hidden : Visibility.Visible;

        switch (Current)
        {
            case Step.Options:
                BtnNext.Content = "Установить"; break;
            case Step.Uninstall:
                BtnNext.Content = InstallService.IsInstalled() ? "Удалить" : "Закрыть"; break;
            case Step.Finish:
                BtnNext.Content = "Готово"; break;
            case Step.Progress:
                break;
            default:
                BtnNext.Content = "Далее"; break;
        }

        BtnNext.Visibility = Current == Step.Progress ? Visibility.Hidden : Visibility.Visible;
        BtnNext.IsEnabled = !(Current == Step.License && LicenseAccept.IsChecked != true);
    }

    private void LicenseAccept_Changed(object sender, RoutedEventArgs e) => UpdateNav();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выберите папку установки",
            InitialDirectory = System.IO.Directory.Exists(PathBox.Text)
                ? PathBox.Text
                : AppInfo.DefaultInstallDir,
        };
        if (dlg.ShowDialog() == true) PathBox.Text = dlg.FolderName;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0) { _index--; ShowCurrent(); }
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        // Завершающий экран — «Готово»
        if (Current == Step.Finish)
        {
            FinalizeAndClose();
            return;
        }

        // Экран удаления
        if (Current == Step.Uninstall)
        {
            if (!InstallService.IsInstalled()) { Close(); return; }
            GoTo(Step.Progress);
            await RunUninstallAsync();
            return;
        }

        // Запуск установки с экрана опций
        if (Current == Step.Options)
        {
            GoTo(Step.Progress);
            await RunInstallAsync();
            return;
        }

        // Обычный переход вперёд
        if (_index < _sequence.Count - 1) { _index++; ShowCurrent(); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void GoTo(Step step)
    {
        _index = _sequence.IndexOf(step);
        ShowCurrent();
    }

    private async Task RunInstallAsync()
    {
        _busy = true;
        ProgressTitle.Text = "Установка Simryx Hub";
        Bar.IsIndeterminate = true;

        var opts = new InstallOptions
        {
            InstallDir = string.IsNullOrWhiteSpace(PathBox.Text) ? AppInfo.DefaultInstallDir : PathBox.Text.Trim(),
            DesktopShortcut = OptDesktop.IsChecked == true,
            RunAtStartup = OptStartup.IsChecked == true,
        };
        var progress = new Progress<ProgressReport>(ReportProgress);

        try
        {
            _result = await _service.InstallAsync(opts, progress);
            FinishTitle.Text = "Установка завершена";
            // Показываем реальную папку установки (установщик добавляет подпапку Simryx Hub).
            FinishText.Text = $"Simryx Hub {_result.Version} установлен в:\n{_result.InstallDir}";
            LaunchNow.Visibility = Visibility.Visible;
            _busy = false;
            GoTo(Step.Finish);
        }
        catch (Exception ex)
        {
            _busy = false;
            ShowFailure("Не удалось завершить установку", ex.Message);
        }
    }

    private async Task RunUninstallAsync()
    {
        _busy = true;
        var purge = PurgeData.IsChecked == true;
        ProgressTitle.Text = "Удаление Simryx Hub";
        Bar.IsIndeterminate = true;
        var progress = new Progress<ProgressReport>(ReportProgress);

        try
        {
            _uninstallDir = await _service.UninstallAsync(progress, purge);
            FinishTitle.Text = "Удаление завершено";
            FinishText.Text = purge
                ? "Simryx Hub и все его настройки, профили и данные удалены с компьютера."
                : "Simryx Hub удалён с компьютера. Ваши настройки и профили сохранены.";
            LaunchNow.Visibility = Visibility.Collapsed;
            _busy = false;
            GoTo(Step.Finish);
        }
        catch (Exception ex)
        {
            _busy = false;
            ShowFailure("Не удалось завершить удаление", ex.Message);
        }
    }

    private void ReportProgress(ProgressReport r)
    {
        ProgressStatus.Text = r.Message;
        if (r.Percent >= 0)
        {
            Bar.IsIndeterminate = false;
            Bar.Value = r.Percent;
        }
        else
        {
            Bar.IsIndeterminate = true;
        }
    }

    private void ShowFailure(string title, string message)
    {
        FinishTitle.Text = title;
        FinishTitle.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Text");
        FinishText.Text = message;
        LaunchNow.Visibility = Visibility.Collapsed;
        _done = false;
        GoTo(Step.Finish);
        BtnNext.Content = "Закрыть";
    }

    private void FinalizeAndClose()
    {
        // Запуск приложения после установки
        if (!_uninstall && _result != null && LaunchNow.IsChecked == true)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _result.AppExePath,
                    UseShellExecute = true,
                    WorkingDirectory = _result.InstallDir,
                });
            }
            catch { }
        }

        // Удаление папки установки после выхода (деинсталляция)
        if (_uninstall)
            InstallService.ScheduleDirectoryDelete(_uninstallDir);

        _done = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; return; } // не закрываем во время работы
        base.OnClosing(e);
    }
}