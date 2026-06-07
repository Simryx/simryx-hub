namespace Simryx.App.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    void Initialize();
    void SetLanguage(string languageTag);
}