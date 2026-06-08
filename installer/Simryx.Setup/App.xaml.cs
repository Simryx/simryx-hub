using System;
using System.Windows;
using Simryx.Setup.Services;

namespace Simryx.Setup;

public partial class App : Application
{
    public static bool UninstallMode { get; private set; }
    public static bool SilentMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        foreach (var a in e.Args)
        {
            if (string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)) UninstallMode = true;
            if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) SilentMode = true;
        }

        ThemeManager.ApplySystemTheme();

        var win = new MainWindow(UninstallMode);
        win.Show();
    }
}