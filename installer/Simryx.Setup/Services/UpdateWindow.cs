using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Simryx.Setup.Services;

namespace Simryx.Setup;

/// <summary>Небольшое окно прогресса тихого обновления (download → extract → relaunch).</summary>
public sealed class UpdateWindow : Window
{
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private readonly ProgressBar _bar = new()
    {
        Height = 8,
        Minimum = 0,
        Maximum = 100,
        IsIndeterminate = true,
    };

    public UpdateWindow()
    {
        Title = "Обновление Simryx Hub";
        Width = 440;
        Height = 170;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        _status.Text = "Подготовка обновления…";

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "Обновление Simryx Hub",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
        });
        panel.Children.Add(_status);
        panel.Children.Add(_bar);
        Content = panel;
    }

    public async Task RunAsync()
    {
        var dir = App.UpdateDir;
        var zip = App.UpdateZipUrl;
        var version = App.UpdateVersion ?? "";

        try
        {
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(zip))
                throw new InvalidOperationException("Не указаны параметры обновления.");

            // 1) Ждём закрытия приложения (по PID), при таймауте закрываем принудительно.
            _status.Text = "Ожидание закрытия приложения…";
            await WaitForProcessExitAsync(App.UpdatePid, TimeSpan.FromSeconds(15));

            // 2) Скачиваем и ставим новую версию в ту же папку.
            var progress = new Progress<ProgressReport>(r =>
            {
                _status.Text = r.Message;
                if (r.Percent < 0) { _bar.IsIndeterminate = true; }
                else { _bar.IsIndeterminate = false; _bar.Value = r.Percent; }
            });

            var service = new InstallService();
            var appExe = await service.UpdateInPlaceAsync(dir!, zip!, version, App.UpdateSha256, progress);

            // 3) Перезапускаем приложение.
            _status.Text = "Запуск Simryx Hub…";
            var launch = !string.IsNullOrWhiteSpace(appExe) ? appExe : App.UpdateLaunch;
            if (!string.IsNullOrWhiteSpace(launch) && File.Exists(launch))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launch,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(launch)!,
                });
            }
        }
        catch (Exception ex)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = 0;
            _status.Text = "Не удалось обновить: " + ex.Message;
            await Task.Delay(4000);
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private static async Task WaitForProcessExitAsync(int pid, TimeSpan timeout)
    {
        if (pid <= 0) return;

        Process? proc;
        try { proc = Process.GetProcessById(pid); }
        catch { return; } // уже закрыт

        if (proc is null) return;

        var sw = Stopwatch.StartNew();
        while (!proc.HasExited && sw.Elapsed < timeout)
            await Task.Delay(250);

        if (!proc.HasExited)
        {
            try { proc.Kill(true); } catch { /* ignore */ }
            try { await Task.Run(() => proc.WaitForExit(3000)); } catch { /* ignore */ }
        }
    }
}