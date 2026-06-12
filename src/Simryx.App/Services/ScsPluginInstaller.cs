using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Simryx.App.Services;

/// <summary>
/// Авто-установка нашего SCS-плагина телеметрии (simryx_telemetry.dll) в папку
/// плагинов запущенной ETS2/ATS. Копирует только при отсутствии/устаревании файла.
/// Безопасно: при любой ошибке (нет прав, файл занят) просто пропускаем — приложение не падает.
/// Важно: игра грузит плагины при старте, поэтому свежескопированный плагин подхватится
/// при СЛЕДУЮЩЕМ запуске игры.
/// </summary>
public static class ScsPluginInstaller
{
    private const string PluginFileName = "simryx_telemetry.dll";

    /// <summary>
    /// Гарантирует, что плагин лежит в bin\win_x64\plugins запущенной SCS-игры.
    /// processNames — имена процессов игры (без .exe), напр. ["eurotrucks2"].
    /// Возвращает true, если файл на месте (скопирован или уже актуален).
    /// </summary>
    public static bool EnsureInstalledFor(string[] processNames)
    {
        try
        {
            var source = FindSourcePlugin();
            if (source is null) return false;

            var gameExe = FindGameExecutable(processNames);
            if (gameExe is null) return false;

            // <root>\bin\win_x64\eurotrucks2.exe  ->  <root>\bin\win_x64\plugins
            var binDir = Path.GetDirectoryName(gameExe);
            if (string.IsNullOrEmpty(binDir)) return false;

            var pluginsDir = Path.Combine(binDir, "plugins");
            Directory.CreateDirectory(pluginsDir);

            var target = Path.Combine(pluginsDir, PluginFileName);
            if (NeedsCopy(source, target))
                File.Copy(source, target, overwrite: true);

            return File.Exists(target);
        }
        catch
        {
            return false;
        }
    }

    private static bool NeedsCopy(string source, string target)
    {
        if (!File.Exists(target)) return true;
        try
        {
            var s = new FileInfo(source);
            var t = new FileInfo(target);
            return s.Length != t.Length || s.LastWriteTimeUtc > t.LastWriteTimeUtc;
        }
        catch
        {
            return true;
        }
    }

    private static string? FindSourcePlugin()
    {
        // DLL поставляется рядом с приложением (или в подпапке scs/ / Assets\scs\).
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, PluginFileName),
            Path.Combine(AppContext.BaseDirectory, "scs", PluginFileName),
            Path.Combine(AppContext.BaseDirectory, "Assets", "scs", PluginFileName),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindGameExecutable(string[] processNames)
    {
        foreach (var name in processNames)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }

            try
            {
                foreach (var p in procs)
                {
                    try
                    {
                        var path = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            return path;
                    }
                    catch { /* доступ к модулю может быть закрыт — пропускаем */ }
                }
            }
            finally
            {
                foreach (var p in procs) { try { p.Dispose(); } catch { } }
            }
        }
        return null;
    }
}