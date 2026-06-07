using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simryx.App.Services;

/// <summary>
/// Сценарий «скачать и установить обновление»: диалог с прогрессом,
/// проверка целостности, запуск установщика и закрытие приложения.
/// </summary>
public static class UpdateFlow
{
    public static async Task RunAsync(UpdateInfo info, XamlRoot xamlRoot, bool en, ElementTheme theme)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.DownloadUrl)) return;

        var cts = new CancellationTokenSource();
        var finished = false;

        var status = new TextBlock
        {
            Text = en ? "Preparing download…" : "Подготовка загрузки…",
            TextWrapping = TextWrapping.Wrap,
        };
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true };
        var detail = new TextBlock { Opacity = 0.7, FontSize = 12 };

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        panel.Children.Add(status);
        panel.Children.Add(bar);
        panel.Children.Add(detail);

        var dialog = new ContentDialog
        {
            Title = en ? $"Updating to {info.Version}" : $"Обновление до {info.Version}",
            Content = panel,
            CloseButtonText = en ? "Cancel" : "Отмена",
            XamlRoot = xamlRoot,
            RequestedTheme = theme,
        };

        // Закрытие диалога пользователем во время загрузки = отмена.
        dialog.Closing += (_, _) =>
        {
            if (!finished)
            {
                try { cts.Cancel(); } catch { /* уже освобождён */ }
            }
        };

        // Progress создаётся на UI-потоке — колбэки маршалятся обратно на UI.
        var progress = new Progress<UpdateDownloadProgress>(p =>
        {
            status.Text = en ? "Downloading update…" : "Загрузка обновления…";
            if (p.TotalBytes > 0)
            {
                bar.IsIndeterminate = false;
                bar.Value = p.Percent;
                detail.Text = $"{FormatSize(p.BytesReceived, en)} / {FormatSize(p.TotalBytes, en)} · {p.Percent}%";
            }
            else
            {
                detail.Text = FormatSize(p.BytesReceived, en);
            }
        });

        var installer = new UpdateInstaller();
        var downloadTask = installer.DownloadAsync(info, progress, cts.Token);

        // Показываем модально, параллельно ждём загрузку.
        var showTask = dialog.ShowAsync().AsTask();
        var result = await downloadTask;
        finished = true;

        if (result.Success)
        {
            status.Text = en ? "Starting installer…" : "Запуск установщика…";
            bar.IsIndeterminate = false;
            bar.Value = 100;
            detail.Text = en
                ? "The app will close to apply the update."
                : "Приложение закроется для установки обновления.";

            await Task.Delay(600); // дать увидеть 100%
            var launched = installer.LaunchInstallerAndExit(result.FilePath!);

            dialog.Hide();
            cts.Dispose();

            if (!launched)
            {
                // Редкий случай: установщик не запустился, приложение осталось открытым.
                ShowError(panel, dialog,
                    en ? "Couldn't start the installer." : "Не удалось запустить установщик.",
                    en);
                dialog.XamlRoot = xamlRoot;
                _ = dialog.ShowAsync();
            }
            return;
        }

        if (result.Status == UpdateDownloadStatus.Canceled)
        {
            dialog.Hide();
            cts.Dispose();
            return;
        }

        // Ошибка — показываем сообщение и ждём, пока пользователь закроет диалог.
        ShowError(panel, dialog, BuildErrorMessage(result, en), en);
        await showTask;
        cts.Dispose();
    }

    private static void ShowError(StackPanel panel, ContentDialog dialog, string message, bool en)
    {
        panel.Children.Clear();
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        dialog.CloseButtonText = en ? "Close" : "Закрыть";
    }

    private static string BuildErrorMessage(UpdateDownloadResult r, bool en)
    {
        var baseMsg = r.Status == UpdateDownloadStatus.IntegrityFailed
            ? (en ? "Integrity check failed — the file may be corrupted."
                  : "Проверка целостности не пройдена — файл может быть повреждён.")
            : (en ? "Couldn't download the update." : "Не удалось загрузить обновление.");
        return string.IsNullOrWhiteSpace(r.Error) ? baseMsg : $"{baseMsg}\n{r.Error}";
    }

    private static string FormatSize(long bytes, bool en)
    {
        var mb = bytes / 1024d / 1024d;
        return en ? $"{mb:0.0} MB" : $"{mb:0.0} МБ";
    }
}