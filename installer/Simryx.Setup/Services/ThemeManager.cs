using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Simryx.Setup.Services;

internal static class ThemeManager
{
    public static bool IsLight { get; private set; }

    public static void ApplySystemTheme()
    {
        IsLight = ReadSystemUsesLight();
        ApplyPalette(IsLight);
    }

    private static bool ReadSystemUsesLight()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (k?.GetValue("AppsUseLightTheme") is int i) return i != 0;
        }
        catch { }
        return false; // по умолчанию — тёмная
    }

    private static void ApplyPalette(bool light)
    {
        var res = Application.Current.Resources;
        void Set(string key, string hex) =>
            res[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);

        if (light)
        {
            Set("Brush.Bg", "#F4F6F9");
            Set("Brush.Surface", "#FFFFFF");
            Set("Brush.Text", "#171B22");
            Set("Brush.Subtle", "#5B6675");
            Set("Brush.Border", "#E2E7EE");
            Set("Brush.Control", "#EEF1F5");
            Set("Brush.ControlHover", "#E3E8EF");
        }
        else
        {
            Set("Brush.Bg", "#0E1116");
            Set("Brush.Surface", "#161A21");
            Set("Brush.Text", "#E7ECF3");
            Set("Brush.Subtle", "#9AA4B2");
            Set("Brush.Border", "#272E39");
            Set("Brush.Control", "#1E242D");
            Set("Brush.ControlHover", "#28303B");
        }

        Set("Brush.Accent", "#5AB6E8");
        Set("Brush.AccentText", "#04151E");
    }
}