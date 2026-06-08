using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Simryx.App.Services;

/// <summary>
/// Реальное обновление через установщик: приложение закрывается и передаёт
/// тихому SimryxSetup.exe URL нового пакета; установщик ставит и перезапускает.
/// </summary>
public static class UpdateFlow
{
    public static async Task RunAsync(UpdateInfo info, XamlRoot xamlRoot, bool en, ElementTheme theme)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.DownloadUrl))
        {
            await ShowMessageAsync(xamlRoot, theme,
                en ? "Update unavailable" : "Обновление недоступно",
                en ? "No download package was found for this release."
                   : "Для этого релиза не найден пакет загрузки.",
                en);
            return;
        }

        var setupExe = LocateInstaller();

        // Установщика рядом нет — это отладочная/портативная сборка: обновляем вручную.
        if (setupExe is null)
        {
            var ask = new ContentDialog
            {
                Title = en ? "Manual update needed" : "Нужно обновить вручную",
                Content = en
                    ? "Automatic update works only in the installed version. Open the release page to download the latest installer."
                    : "Автообновление работает только в установленной версии. Откройте страницу релиза и скачайте установщик.",
                PrimaryButtonText = en ? "Open release page" : "Открыть страницу релиза",
                CloseButtonText = en ? "Cancel" : "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                RequestedTheme = theme,
            };
            if (await ask.ShowAsync() == ContentDialogResult.Primary &&
                !string.IsNullOrWhiteSpace(info.ReleaseUrl))
            {
                await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
            }
            return;
        }

        // Короткое подтверждение перед закрытием приложения.
        var confirm = new ContentDialog
        {
            Title = en ? $"Update to {info.Version}" : $"Обновить до {info.Version}",
            Content = en
                ? "Simryx Hub will close, install the update and reopen automatically."
                : "Simryx Hub закроется, установит обновление и снова откроется автоматически.",
            PrimaryButtonText = en ? "Update now" : "Обновить сейчас",
            CloseButtonText = en ? "Cancel" : "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = theme,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var installDir = Path.GetDirectoryName(setupExe)!;
        var appExe = Environment.ProcessPath ?? string.Empty;
        var args = BuildArgs(info, installDir, appExe, Environment.ProcessId);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = setupExe,
                Arguments = args,
                UseShellExecute = true, // отдельный процесс — переживёт выход приложения
                WorkingDirectory = Path.GetTempPath(),
            });
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(xamlRoot, theme,
                en ? "Couldn't start the updater" : "Не удалось запустить обновление",
                ex.Message, en);
            return;
        }

        // Полностью закрываемся, чтобы установщик мог заменить файлы.
        App.QuitCompletely();
    }

    private static string BuildArgs(UpdateInfo info, string installDir, string appExe, int pid)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
        var args = $"--update --silent --pid {pid} --dir {Q(installDir)} --zip {Q(info.DownloadUrl!)} --version {Q(info.Version)}";
        if (!string.IsNullOrWhiteSpace(appExe)) args += $" --launch {Q(appExe)}";
        if (!string.IsNullOrWhiteSpace(info.Sha256)) args += $" --sha256 {Q(info.Sha256!)}";
        return args;
    }

    // Ищем SimryxSetup.exe рядом с приложением (вверх по папкам), затем по реестру.
    private static string? LocateInstaller()
    {
        try
        {
            var exe = Environment.ProcessPath;
            var dir = string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
            for (var i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir!, "SimryxSetup.exe");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir!);
            }
        }
        catch { /* ignore */ }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SimryxHub");
            if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
            {
                var candidate = Path.Combine(loc, "SimryxSetup.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { /* ignore */ }

        return null;
    }

    private static async Task ShowMessageAsync(XamlRoot xamlRoot, ElementTheme theme, string title, string message, bool en)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = en ? "Close" : "Закрыть",
            XamlRoot = xamlRoot,
            RequestedTheme = theme,
        };
        await dialog.ShowAsync();
    }
}