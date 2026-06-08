using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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

    /// <summary>Имя нашей подпапки установки. Внутрь именно её ставим и только её чистим.</summary>
    private const string InstallFolderName = "Simryx Hub";

    /// <summary>Папка с пользовательскими данными приложения (%LocalAppData%\Simryx).</summary>
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Simryx");

    public async Task<InstallResult> InstallAsync(
        InstallOptions o, IProgress<ProgressReport> p, CancellationToken ct = default)
    {
        // ВАЖНО: никогда не устанавливаем в «голую» выбранную папку — всегда внутрь
        // собственной подпапки Simryx Hub. Это защищает от очистки корня диска
        // или чужих данных (выбор «D:\» -> «D:\Simryx Hub»).
        var installDir = ResolveInstallDir(o.InstallDir);
        EnsureSafeInstallDir(installDir);

        p.Report(new("Получение сведений о последней версии…", -1));
        var rel = await _gh.GetLatestStableAsync(ct);

        var tmpZip = Path.Combine(Path.GetTempPath(), $"SimryxHub_{Guid.NewGuid():N}.zip");
        p.Report(new($"Скачивание Simryx Hub {rel.Version}…", 0));
        await _gh.DownloadAsync(rel.DownloadUrl, tmpZip,
            new Progress<double>(d => p.Report(new($"Скачивание Simryx Hub {rel.Version}…", d))), ct);

        p.Report(new("Подготовка папки установки…", -1));
        Directory.CreateDirectory(installDir);
        CleanDirContents(installDir);

        p.Report(new("Распаковка файлов…", -1));
        ZipFile.ExtractToDirectory(tmpZip, installDir, overwriteFiles: true);
        try { File.Delete(tmpZip); } catch { }

        var appExe = FindAppExe(installDir)
            ?? throw new Exception("В пакете не найден исполняемый файл приложения.");
        var appDir = Path.GetDirectoryName(appExe) ?? installDir;

        p.Report(new("Копирование деинсталлятора…", -1));
        var selfExe = Environment.ProcessPath!;
        var uninstExe = Path.Combine(installDir, "SimryxSetup.exe");
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
            appExe, AppInfo.Description, appExe, appDir);
        Native.CreateShortcut(Path.Combine(startMenuDir, "Удалить " + AppInfo.ProductName + ".lnk"),
            uninstExe, "Удаление " + AppInfo.ProductName, uninstExe, installDir, "--uninstall");

        if (o.DesktopShortcut)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Native.CreateShortcut(Path.Combine(desktop, AppInfo.ProductName + ".lnk"),
                appExe, AppInfo.Description, appExe, appDir);
        }

        p.Report(new("Запись в реестр…", -1));
        WriteUninstallRegistry(installDir, appExe, uninstExe, rel.Version);
        SetRunAtStartup(o.RunAtStartup, appExe);

        p.Report(new("Готово", 100));
        return new InstallResult { AppExePath = appExe, Version = rel.Version, InstallDir = installDir };
    }

    /// <summary>
    /// Обновление на месте: качаем переданный ZIP, проверяем целостность, чистим
    /// и распаковываем в ту же папку, пересоздаём ярлыки и обновляем версию в реестре.
    /// Настройки автозапуска и ярлык на рабочем столе НЕ трогаем — сохраняем выбор пользователя.
    /// Возвращает путь к новому exe приложения.
    /// </summary>
    public async Task<string> UpdateInPlaceAsync(
        string installDir, string zipUrl, string version, string? sha256,
        IProgress<ProgressReport> p, CancellationToken ct = default)
    {
        var dir = ResolveInstallDir(installDir);
        EnsureSafeInstallDir(dir);

        var tmpZip = Path.Combine(Path.GetTempPath(), $"SimryxHub_{Guid.NewGuid():N}.zip");

        // Сначала полностью качаем и проверяем во временный файл — пока не тронули установку.
        p.Report(new($"Скачивание Simryx Hub {version}…", 0));
        await _gh.DownloadAsync(zipUrl, tmpZip,
            new Progress<double>(d => p.Report(new($"Скачивание Simryx Hub {version}…", d))), ct);

        if (!string.IsNullOrWhiteSpace(sha256))
        {
            p.Report(new("Проверка целостности…", -1));
            var actual = ComputeSha256(tmpZip);
            if (!string.Equals(actual, sha256!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(tmpZip); } catch { }
                throw new Exception("Проверка целостности не пройдена — файл повреждён.");
            }
        }

        p.Report(new("Подготовка папки установки…", -1));
        Directory.CreateDirectory(dir);
        CleanDirContents(dir);

        p.Report(new("Распаковка файлов…", -1));
        ZipFile.ExtractToDirectory(tmpZip, dir, overwriteFiles: true);
        try { File.Delete(tmpZip); } catch { }

        var appExe = FindAppExe(dir)
            ?? throw new Exception("В пакете не найден исполняемый файл приложения.");
        var appDir = Path.GetDirectoryName(appExe) ?? dir;

        // Деинсталлятор — это мы сами; в папке он уже есть и при обновлении не перезаписывается.
        var uninstExe = Path.Combine(dir, "SimryxSetup.exe");
        var selfExe = Environment.ProcessPath!;
        try
        {
            if (!string.Equals(selfExe, uninstExe, StringComparison.OrdinalIgnoreCase))
                File.Copy(selfExe, uninstExe, true);
        }
        catch { uninstExe = selfExe; }

        p.Report(new("Обновление ярлыков…", -1));
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.ProductName);
        Directory.CreateDirectory(startMenuDir);
        Native.CreateShortcut(Path.Combine(startMenuDir, AppInfo.ProductName + ".lnk"),
            appExe, AppInfo.Description, appExe, appDir);
        Native.CreateShortcut(Path.Combine(startMenuDir, "Удалить " + AppInfo.ProductName + ".lnk"),
            uninstExe, "Удаление " + AppInfo.ProductName, uninstExe, dir, "--uninstall");

        p.Report(new("Обновление записей реестра…", -1));
        WriteUninstallRegistry(dir, appExe, uninstExe, version);

        p.Report(new("Готово", 100));
        return appExe;
    }

    public async Task<string> UninstallAsync(
        IProgress<ProgressReport> p, bool purgeData, CancellationToken ct = default)
    {
        await Task.Yield();
        var dir = ReadInstallLocation();

        p.Report(new("Удаление ярлыков…", -1));
        RemoveShortcuts();

        p.Report(new("Отключение автозапуска…", -1));
        SetRunAtStartup(false, null);

        p.Report(new("Очистка записей реестра…", -1));
        RemoveUninstallRegistry();

        if (purgeData)
        {
            p.Report(new("Удаление настроек, профилей и данных…", -1));
            PurgeUserData();
        }

        p.Report(new("Готово", 100));
        return dir;
    }

    /// <summary>Полностью удаляет пользовательские данные (%LocalAppData%\Simryx).</summary>
    public static void PurgeUserData()
    {
        var dir = DataDir;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        try
        {
            Directory.Delete(dir, true);
        }
        catch
        {
            // Если что-то заблокировано — попробуем удалить отложенно после выхода.
            ScheduleDirectoryDelete(dir);
        }
    }

    /// <summary>Удаляет папку установки после выхода процесса (через отложенный cmd).</summary>
    public static void ScheduleDirectoryDelete(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        // Не планируем удаление опасных путей (корень диска и т.п.).
        if (!IsDeletablePath(dir)) return;

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

    // ===== Безопасность путей =====

    /// <summary>
    /// Приводит выбранный путь к безопасной папке установки: всегда добавляет
    /// подпапку Simryx Hub, если её ещё нет. Так выбор «D:\» превращается в
    /// «D:\Simryx Hub», а «D:\123» — в «D:\123\Simryx Hub».
    /// </summary>
    public static string ResolveInstallDir(string? chosen)
    {
        if (string.IsNullOrWhiteSpace(chosen))
            return AppInfo.DefaultInstallDir;

        string full;
        try { full = Path.GetFullPath(chosen.Trim()); }
        catch { return AppInfo.DefaultInstallDir; }

        var name = new DirectoryInfo(full).Name;
        if (string.Equals(name, InstallFolderName, StringComparison.OrdinalIgnoreCase))
            return full;

        return Path.Combine(full, InstallFolderName);
    }

    /// <summary>Бросает исключение, если папка установки опасна (корень диска / системная).</summary>
    private static void EnsureSafeInstallDir(string dir)
    {
        var full = Path.GetFullPath(dir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = (Path.GetPathRoot(full) ?? "")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Корень диска (например, «D:»)
        if (full.Length <= root.Length || full.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Недопустимая папка установки (корень диска). Выберите обычную папку.");

        // Системные папки Windows
        foreach (var b in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        })
        {
            var bb = (b ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (bb.Length > 0 &&
                (full.Equals(bb, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(bb + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "Недопустимая папка установки (системная папка Windows).");
        }
    }

    /// <summary>Путь можно безопасно удалять рекурсивно? (не корень диска и не системная папка)</summary>
    private static bool IsDeletablePath(string dir)
    {
        try
        {
            var full = Path.GetFullPath(dir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = (Path.GetPathRoot(full) ?? "")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length <= root.Length || full.Equals(root, StringComparison.OrdinalIgnoreCase))
                return false;

            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (windows.Length > 0 &&
                (full.Equals(windows, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }
        catch { return false; }
    }

    // ===== helpers =====

    private static string? FindAppExe(string dir)
    {
        // Сначала ищем по известным именам — включая вложенные папки.
        foreach (var name in new[] { "Simryx.Hub.exe", "Simryx.App.exe" })
        {
            var hit = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null) return hit;
        }

        // Запасной вариант: любой подходящий .exe (рекурсивно), кроме служебных.
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SimryxSetup.exe", "createdump.exe",
        };
        return Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(f => !skip.Contains(Path.GetFileName(f)));
    }

    private static void CleanDirContents(string dir)
    {
        if (!Directory.Exists(dir)) return;

        // ЗАЩИТА ОТ ПОТЕРИ ДАННЫХ: чистим ТОЛЬКО собственную папку установки
        // (с именем Simryx Hub) и никогда — корень диска или произвольную папку.
        var full = Path.GetFullPath(dir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = (Path.GetPathRoot(full) ?? "")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Length <= root.Length || full.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Отказано: очистка корня диска недопустима.");
        if (!string.Equals(new DirectoryInfo(full).Name, InstallFolderName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Отказано: папка не является каталогом установки Simryx Hub.");

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

    private static string ComputeSha256(string file)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(file);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
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