using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simryx.App.Services;

namespace Simryx.App.Views;

public sealed partial class WelcomeDialog : ContentDialog
{
    private readonly IThemeSelectorService _theme;
    private readonly ILocalizationService _localization;

    /// <summary>True, если пользователь сменил язык — нужен перезапуск для полного применения.</summary>
    public bool RestartForLanguage { get; private set; }

    public WelcomeDialog()
    {
        InitializeComponent();

        _theme = App.Services.GetRequiredService<IThemeSelectorService>();
        _localization = App.Services.GetRequiredService<ILocalizationService>();

        // Предвыбор текущего языка
        var lang = _localization.CurrentLanguage ?? string.Empty;
        LanguageBox.SelectedIndex =
            lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        // Предвыбор текущей темы
        ThemeButtons.SelectedIndex = _theme.Theme switch
        {
            ElementTheme.Light => 0,
            ElementTheme.Dark => 1,
            _ => 2,
        };

        PrimaryButtonClick += OnStart;
    }

    private void OnStart(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Тема — применяется сразу
        if (ThemeButtons.SelectedItem is FrameworkElement themeItem &&
            themeItem.Tag is string themeTag)
        {
            var theme = themeTag switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            _theme.SetTheme(theme);
            _theme.ApplyTheme();
        }

        // Язык — сохраняется; если изменился, нужен перезапуск для полного применения (включая левое меню)
        if (LanguageBox.SelectedItem is FrameworkElement langItem &&
            langItem.Tag is string langTag)
        {
            var current = string.IsNullOrEmpty(_localization.CurrentLanguage)
                ? "ru-RU"
                : _localization.CurrentLanguage;

            if (!string.Equals(current, langTag, StringComparison.OrdinalIgnoreCase))
            {
                _localization.SetLanguage(langTag);
                RestartForLanguage = true;
            }
        }

        OnboardingService.MarkDone();
    }
}