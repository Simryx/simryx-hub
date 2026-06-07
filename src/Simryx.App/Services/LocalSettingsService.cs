using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Simryx.App.Services;

public sealed class LocalSettingsService : ILocalSettingsService
{
    private readonly string _filePath;
    private readonly Dictionary<string, JsonElement> _cache;

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
        var json = JsonSerializer.Serialize(_cache,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}