using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Simryx.App.Services;

public sealed class LocalSettingsService : ILocalSettingsService
{
    private readonly string _filePath;
    private Dictionary<string, JsonElement> _cache;

    public LocalSettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simryx", "Simryx Hub");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        _cache = Load();
    }

    private Dictionary<string, JsonElement> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                       ?? new Dictionary<string, JsonElement>();
            }
        }
        catch
        {
            // повреждённый файл настроек — игнорируем и начинаем с чистого
        }
        return new Dictionary<string, JsonElement>();
    }

    public T? Read<T>(string key)
    {
        if (_cache.TryGetValue(key, out var element))
        {
            try { return element.Deserialize<T>(); }
            catch { return default; }
        }
        return default;
    }

    public void Save<T>(string key, T value)
    {
        _cache[key] = JsonSerializer.SerializeToElement(value);
        WriteCache();
    }

    /// <summary>Перечитывает настройки с диска (после импорта/внешней замены файла).</summary>
    public void Reload() => _cache = Load();

    /// <summary>
    /// Атомарная запись: сначала во временный файл, затем подмена основного.
    /// Это защищает settings.json от повреждения, если запись прервётся
    /// (вылет, отключение питания) посреди процесса.
    /// </summary>
    private void WriteCache()
    {
        var json = JsonSerializer.Serialize(_cache,
            new JsonSerializerOptions { WriteIndented = true });

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);

        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, null);
        else
            File.Move(tmp, _filePath);
    }
}