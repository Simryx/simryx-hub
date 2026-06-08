namespace Simryx.App.Services;

public interface ILocalSettingsService
{
    T? Read<T>(string key);
    void Save<T>(string key, T value);

    /// <summary>
    /// Перечитывает настройки с диска в кэш. Нужно после внешней замены
    /// settings.json (например, импорта), иначе следующий Save перезапишет
    /// файл устаревшими значениями из памяти.
    /// Default-реализация — пустая, чтобы не ломать другие реализации интерфейса.
    /// </summary>
    void Reload() { }
}