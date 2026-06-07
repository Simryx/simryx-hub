using Microsoft.Windows.Globalization;

namespace Simryx.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const string SettingsKey = "AppLanguage";
    private readonly ILocalSettingsService _settings;

    public string CurrentLanguage { get; private set; } = "ru-RU";

    public LocalizationService(ILocalSettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize()
    {
        var stored = _settings.Read<string>(SettingsKey);
        CurrentLanguage = string.IsNullOrWhiteSpace(stored) ? "ru-RU" : stored!;
        ApplicationLanguages.PrimaryLanguageOverride = CurrentLanguage;
    }

    public void SetLanguage(string languageTag)
    {
        CurrentLanguage = languageTag;
        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        _settings.Save(SettingsKey, languageTag);
    }
}