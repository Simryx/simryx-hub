using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Simryx.Setup.Services;

public record ProgressReport(string Message, double Percent); // Percent < 0 => неопределённый

public sealed class InstallOptions
{
    public string InstallDir { get; init; } = AppInfo.DefaultInstallDir;
    public bool DesktopShortcut { get; init; } = true;
    public bool RunAtStartup { get; init; }
}

public sealed class InstallResult
{
    public string AppExePath { get; init; } = "";
    public string Version { get; init; } = "";
    public string InstallDir { get; init; } = "";
}

public sealed class InstallService
{
    private readonly GitHubClient _gh = new();

    public async Task<InstallResult> InstallAsync(
        InstallOptions o, IProgress<ProgressReport> p, CancellationToken ct = default)
    {
        p.Report(new("Получение сведений о последней версии…", -1));
        var rel = await _gh.GetLatestStableAsync(ct);

        var tmpZip = Path.Combine(Path.GetTempPath(), $"SimryxHub_{Guid.NewGuid():N}.zip");
        p.Report(new($"Скачивание Simryx Hub {rel.Version}…", 0));
        await _gh.DownloadAsync(rel.DownloadUrl, tmpZip,
            new Progress<double>(d => p.Report(new($"Скачивание Simryx Hub {rel.Version}…", d))), ct);

        p.Report(new("Подготовка папки установки…", -1));
        Directory.CreateDirectory(o.InstallDir);
        CleanDirContents(o.InstallDir);

        p.Report(new("Распаковка файлов…", -1));
        ZipFile.ExtractToDirectory(tmpZip, o.InstallDir, overwriteFiles: true);
        try { File.Delete(tmpZip); } catch { }

        var appExe = FindAppExe(o.InstallDir)
            ?? throw new Exception("В пакете не найден исполняемый файл приложения.");

        p.Report(new("Копирование деинсталлятора…", -1));
        var selfExe = Environment.ProcessPath!;
        var uninstExe = Path.Combine(o.InstallDir, "SimryxSetup.exe");
        try
        {
            if (!string.Equals(selfExe, uninstExe, StringComparison.OrdinalIgnoreCase))
                File.Copy(selfExe, uninstExe, true);
        }
        catch { uninstExe = selfExe; }

        p.Report(new("Создание ярлыков…", -1));
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.ProductName);
        Directory.CreateDirectory(startMenuDir);

        Native.CreateShortcut(Path.Combine(startMenuDir, AppInfo.ProductName + ".lnk"),
            appExe, AppInfo.Description, appExe, o.InstallDir);
        Native.CreateShortcut(Path.Combine(startMenuDir, "Удалить " + AppInfo.ProductName + ".lnk"),
            uninstExe, "Удаление " + AppInfo.ProductName, uninstExe, o.InstallDir, "--uninstall");

        if (o.DesktopShortcut)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Native.CreateShortcut(Path.Combine(desktop, AppInfo.ProductName + ".lnk"),
                appExe, AppInfo.Description, appExe, o.InstallDir);
        }

        p.Report(new("Запись в реестр…", -1));
        WriteUninstallRegistry(o.InstallDir, appExe, uninstExe, rel.Version);
        SetRunAtStartup(o.RunAtStartup, appExe);

        p.Report(new("Готово", 100));
        return new InstallResult { AppExePath = appExe, Version = rel.Version, InstallDir = o.InstallDir };
    }

    public async Task<string> UninstallAsync(IProgress<ProgressReport> p, CancellationToken ct = default)
    {
        await Task.Yield();
        var dir = ReadInstallLocation();

        p.Report(new("Удаление ярлыков…", -1));
        RemoveShortcuts();

        p.Report(new("Отключение автозапуска…", -1));
        SetRunAtStartup(false, null);

        p.Report(new("Очистка записей реестра…", -1));
        RemoveUninstallRegistry();

        p.Report(new("Готово", 100));
        return dir;
    }

    /// <summary>Удаляет папку установки после выхода процесса (через отложенный cmd).</summary>
    public static void ScheduleDirectoryDelete(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{dir}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        };
        try { Process.Start(psi); } catch { }
    }

    public static string ReadInstallLocation()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKey);
            if (k?.GetValue("InstallLocation") is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch { }
        return AppInfo.DefaultInstallDir;
    }

    public static bool IsInstalled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKey);
        return k is not null;
    }

    // ===== helpers =====

    private static string? FindAppExe(string dir)
    {
        foreach (var name in new[] { "Simryx.Hub.exe", "Simryx.App.exe" })
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return Directory.EnumerateFiles(dir, "*.exe").FirstOrDefault(f =>
        {
            var n = Path.GetFileName(f);
            return !n.Equals("SimryxSetup.exe", StringComparison.OrdinalIgnoreCase)
                && !n.Equals("createdump.exe", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void CleanDirContents(string dir)
    {
        if (!Directory.Exists(dir)) return;
        var self = Environment.ProcessPath ?? "";
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            if (string.Equals(f, self, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(f); } catch { }
        }
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            try { Directory.Delete(d, true); } catch { }
        }
    }

    private static void WriteUninstallRegistry(string dir, string appExe, string uninstExe, string version)
    {
        using var k = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallKey);
        k.SetValue("DisplayName", AppInfo.ProductName);
        k.SetValue("DisplayVersion", version);
        k.SetValue("Publisher", AppInfo.Publisher);
        k.SetValue("DisplayIcon", appExe);
        k.SetValue("InstallLocation", dir);
        k.SetValue("UninstallString", $"\"{uninstExe}\" --uninstall");
        k.SetValue("QuietUninstallString", $"\"{uninstExe}\" --uninstall --silent");
        k.SetValue("URLInfoAbout", AppInfo.AboutUrl);
        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try { k.SetValue("EstimatedSize", DirSizeKb(dir), RegistryValueKind.DWord); } catch { }
    }

    private static void RemoveUninstallRegistry()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallKey, false); } catch { }
    }

    public static void SetRunAtStartup(bool enable, string? appExe)
    {
        using var k = Registry.CurrentUser.CreateSubKey(AppInfo.RunKey);
        if (enable && !string.IsNullOrEmpty(appExe)) k.SetValue(AppInfo.RunValueName, $"\"{appExe}\"");
        else try { k.DeleteValue(AppInfo.RunValueName, false); } catch { }
    }

    private static void RemoveShortcuts()
    {
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.ProductName);
        try { if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true); } catch { }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var lnk = Path.Combine(desktop, AppInfo.ProductName + ".lnk");
        try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
    }

    private static int DirSizeKb(string dir)
    {
        long bytes = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { bytes += new FileInfo(f).Length; } catch { }
        }
        return (int)Math.Min(int.MaxValue, bytes / 1024);
    }
}