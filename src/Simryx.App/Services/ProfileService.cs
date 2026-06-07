using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Simryx.App.Models;

namespace Simryx.App.Services;

public sealed class ProfileService
{
    private static readonly string Dir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simryx", "Simryx Hub");

    private static string ProfilesPath => Path.Combine(Dir, "profiles.json");
    private static string PrioritiesPath => Path.Combine(Dir, "priorities.json");
    private static string ActivePath => Path.Combine(Dir, "active-profile.txt");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Срабатывает при любом изменении набора профилей или приоритетов.
    public static event Action? Changed;

    // Совместимость с главной страницей (показывает текущий профиль).
    public static event Action? ActiveChanged;

    // ===== Профили =====

    public List<RacingProfile> GetAll()
    {
        try
        {
            if (!File.Exists(ProfilesPath)) return new List<RacingProfile>();
            var list = JsonSerializer.Deserialize<List<RacingProfile>>(File.ReadAllText(ProfilesPath));
            return list ?? new List<RacingProfile>();
        }
        catch
        {
            return new List<RacingProfile>();
        }
    }

    public void SaveAll(IEnumerable<RacingProfile> profiles)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(profiles.ToList(), JsonOptions));
        Changed?.Invoke();
    }

    public RacingProfile Add(RacingProfile profile)
    {
        var all = GetAll();
        profile.CreatedAt = DateTime.Now;
        profile.UpdatedAt = DateTime.Now;
        all.Add(profile);
        SaveAll(all);
        return profile;
    }

    public void Update(RacingProfile profile)
    {
        var all = GetAll();
        var idx = all.FindIndex(p => p.Id == profile.Id);
        if (idx < 0) return;
        profile.UpdatedAt = DateTime.Now;
        all[idx] = profile;
        SaveAll(all);
    }

    public void Delete(string id)
    {
        var all = GetAll();
        all.RemoveAll(p => p.Id == id);
        SaveAll(all);

        // Убираем профиль из приоритетов, если он там был.
        var priorities = GetPriorities();
        var keys = priorities.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList();
        if (keys.Count > 0)
        {
            foreach (var key in keys) priorities.Remove(key);
            SavePriorities(priorities);
        }

        if (GetActiveId() == id) SetActiveInternal(null);
    }

    // ===== Приоритеты по играм =====

    public Dictionary<string, string> GetPriorities()
    {
        try
        {
            if (!File.Exists(PrioritiesPath)) return new Dictionary<string, string>();
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PrioritiesPath));
            return dict ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private void SavePriorities(Dictionary<string, string> priorities)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PrioritiesPath, JsonSerializer.Serialize(priorities, JsonOptions));
    }

    public string? GetPriorityId(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return null;
        return GetPriorities().TryGetValue(gameId, out var id) ? id : null;
    }

    // Делает профиль приоритетным в своей игре и текущим активным для главной.
    public void SetPriority(string gameId, string profileId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(profileId)) return;
        var priorities = GetPriorities();
        priorities[gameId] = profileId;
        SavePriorities(priorities);
        SetActiveInternal(profileId);
        Changed?.Invoke();
    }

    // ===== Текущий (активный) профиль для главной =====

    public string? GetActiveId()
    {
        try
        {
            return File.Exists(ActivePath) ? File.ReadAllText(ActivePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public RacingProfile? GetActive()
    {
        var id = GetActiveId();
        if (string.IsNullOrWhiteSpace(id)) return null;
        return GetAll().FirstOrDefault(p => p.Id == id);
    }

    public void SetActive(string id) => SetActiveInternal(id);

    private void SetActiveInternal(string? id)
    {
        Directory.CreateDirectory(Dir);
        if (string.IsNullOrWhiteSpace(id))
        {
            if (File.Exists(ActivePath)) File.Delete(ActivePath);
        }
        else
        {
            File.WriteAllText(ActivePath, id);
        }
        ActiveChanged?.Invoke();
    }
}