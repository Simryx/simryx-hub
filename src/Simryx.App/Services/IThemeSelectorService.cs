using Microsoft.UI.Xaml;

namespace Simryx.App.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme { get; }
    void Initialize();
    void SetTheme(ElementTheme theme);
    void ApplyTheme();
}