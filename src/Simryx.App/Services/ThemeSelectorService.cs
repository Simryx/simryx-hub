using System;
using Microsoft.UI.Xaml;

namespace Simryx.App.Services;

public sealed class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppTheme";
    private readonly ILocalSettingsService _settings;

    public ElementTheme Theme { get; private set; } = ElementTheme.Default;

    public ThemeSelectorService(ILocalSettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize()
    {
        var stored = _settings.Read<string>(SettingsKey);
        Theme = Enum.TryParse(stored, out ElementTheme t) ? t : ElementTheme.Default;
    }

    public void SetTheme(ElementTheme theme)
    {
        Theme = theme;
        ApplyTheme();
        _settings.Save(SettingsKey, theme.ToString());
    }

    public void ApplyTheme()
    {
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = Theme;
        }
    }
}