using System;
using System.Windows;
using Simryx.Setup.Services;

namespace Simryx.Setup;

public partial class App : Application
{
    public static bool UninstallMode { get; private set; }
    public static bool SilentMode { get; private set; }
    public static bool UpdateMode { get; private set; }

    public static int UpdatePid { get; private set; }
    public static string? UpdateDir { get; private set; }
    public static string? UpdateZipUrl { get; private set; }
    public static string? UpdateVersion { get; private set; }
    public static string? UpdateLaunch { get; private set; }
    public static string? UpdateSha256 { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ParseArgs(e.Args);
        ThemeManager.ApplySystemTheme();

        // Тихое обновление: без мастера, маленькое окно прогресса.
        if (UpdateMode)
        {
            var updateWin = new UpdateWindow();
            updateWin.Show();
            _ = updateWin.RunAsync(); // сам перезапустит приложение и закроет процесс
            return;
        }

        var win = new MainWindow(UninstallMode);
        win.Show();
    }

    private static void ParseArgs(string[] args)
    {
        static string? ValueAfter(string[] a, ref int idx) => idx + 1 < a.Length ? a[++idx] : null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--uninstall": UninstallMode = true; break;
                case "--silent": SilentMode = true; break;
                case "--update": UpdateMode = true; break;
                case "--pid": if (int.TryParse(ValueAfter(args, ref i), out var pid)) UpdatePid = pid; break;
                case "--dir": UpdateDir = ValueAfter(args, ref i); break;
                case "--zip": UpdateZipUrl = ValueAfter(args, ref i); break;
                case "--version": UpdateVersion = ValueAfter(args, ref i); break;
                case "--launch": UpdateLaunch = ValueAfter(args, ref i); break;
                case "--sha256": UpdateSha256 = ValueAfter(args, ref i); break;
            }
        }
    }
}