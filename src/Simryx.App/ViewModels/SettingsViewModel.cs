using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simryx.App.Services;

namespace Simryx.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeSelectorService _theme;

    [ObservableProperty]
    private ElementTheme elementTheme;

    public SettingsViewModel(IThemeSelectorService theme)
    {
        _theme = theme;
        elementTheme = _theme.Theme;
    }

    [RelayCommand]
    private void SetTheme(ElementTheme theme)
    {
        ElementTheme = theme;
        _theme.SetTheme(theme);
    }
}