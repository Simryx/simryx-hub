namespace Simryx.App.Services;

public interface ILocalSettingsService
{
    T? Read<T>(string key);
    void Save<T>(string key, T value);
}