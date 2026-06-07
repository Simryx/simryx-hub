using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Simryx.App.Services;

/// <summary>
/// Загрузка и запуск установщика обновления.
/// Файл скачивается с GitHub Releases, проверяется на целостность
/// (размер и SHA-256, если он опубликован), затем запускается установщик
/// и закрывается приложение, чтобы он мог заменить файлы.
/// </summary>
public sealed class UpdateInstaller
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        // Без общего таймаута: большие файлы; отмена — через CancellationToken.
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SimryxHub-Updater");
        return http;
    }

    private static string DownloadDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Simryx", "Simryx Hub", "updates");

    /// <summary>Скачать установщик из релиза с отчётом о прогрессе и проверкой целостности.</summary>
    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateInfo info,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.DownloadUrl))
            return Fail("Для этого релиза не приложен установщик.");

        var tmp = string.Empty;
        try
        {
            Directory.CreateDirectory(DownloadDir);

            var fileName = string.IsNullOrWhiteSpace(info.AssetName)
                ? $"SimryxHub-{info.Version}.exe"
                : info.AssetName!;
            var target = Path.Combine(DownloadDir, fileName);
            tmp = target + ".part";

            using (var resp = await Http.GetAsync(
                info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength
                            ?? (info.AssetSize > 0 ? info.AssetSize : 0);

                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(
                    tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    progress?.Report(new UpdateDownloadProgress
                    {
                        BytesReceived = received,
                        TotalBytes = total,
                    });
                }
            }

            // 1) Проверка целостности по размеру.
            if (info.AssetSize > 0)
            {
                var len = new FileInfo(tmp).Length;
                if (len != info.AssetSize)
                {
                    TryDelete(tmp);
                    return new UpdateDownloadResult
                    {
                        Status = UpdateDownloadStatus.IntegrityFailed,
                        Error = $"Размер файла не совпал (ожидалось {info.AssetSize}, получено {len}).",
                    };
                }
            }

            // 2) Проверка целостности по SHA-256 (если опубликована в заметках релиза).
            if (!string.IsNullOrWhiteSpace(info.Sha256))
            {
                var actual = await Task.Run(() => ComputeSha256(tmp), ct);
                if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(tmp);
                    return new UpdateDownloadResult
                    {
                        Status = UpdateDownloadStatus.IntegrityFailed,
                        Error = "Контрольная сумма SHA-256 не совпала.",
                    };
                }
            }

            if (File.Exists(target)) TryDelete(target);
            File.Move(tmp, target);

            return new UpdateDownloadResult
            {
                Status = UpdateDownloadStatus.Completed,
                FilePath = target,
            };
        }
        catch (OperationCanceledException)
        {
            TryDelete(tmp);
            return new UpdateDownloadResult { Status = UpdateDownloadStatus.Canceled };
        }
        catch (Exception ex)
        {
            TryDelete(tmp);
            return Fail(ex.Message);
        }
    }

    /// <summary>Запустить установщик и закрыть приложение (чтобы он мог заменить файлы).</summary>
    public bool LaunchInstallerAndExit(string installerPath)
    {
        try
        {
            if (!File.Exists(installerPath)) return false;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true, // .exe запустится, .msix/.zip откроются через оболочку
            };
            System.Diagnostics.Process.Start(psi);

            // Закрываем приложение, чтобы установщик мог обновить файлы.
            App.MainWindow?.Close();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
        catch { /* игнорируем */ }
    }

    private static UpdateDownloadResult Fail(string error) =>
        new() { Status = UpdateDownloadStatus.Failed, Error = error };
}