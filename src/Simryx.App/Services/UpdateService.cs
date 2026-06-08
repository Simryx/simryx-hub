using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Simryx.App.Services;

/// <summary>
/// Проверка обновлений приложения через GitHub Releases.
/// Репозиторий публичный, поэтому токен не требуется.
/// </summary>
public sealed class UpdateService
{
    public const string Owner = "Simryx";
    public const string Repo = "simryx-hub";
    private const string ApiBase = "https://api.github.com";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub API требует User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SimryxHub-Updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    /// <summary>Текущая версия приложения (из метаданных сборки).</summary>
    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0, 0);

    /// <summary>
    /// Является ли ТЕКУЩАЯ установленная сборка пред-релизом (beta/rc).
    /// AssemblyVersion числовой и суффикс не хранит, поэтому смотрим
    /// AssemblyInformationalVersion (туда из &lt;Version&gt; попадает полный semver,
    /// например "0.2.2-beta.1"). "+&lt;git-hash&gt;" отбрасываем, чтобы не спутать с дефисом.
    /// </summary>
    public static bool CurrentIsPrerelease()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info)) return false;
        var plus = info.IndexOf('+');
        if (plus >= 0) info = info[..plus];
        return info.Contains('-');
    }

    /// <summary>Проверить наличие обновления в выбранном канале.</summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdateChannel channel = UpdateChannel.Stable,
        CancellationToken ct = default)
    {
        var current = CurrentVersion;
        try
        {
            var release = channel == UpdateChannel.Beta
                ? await GetLatestIncludingPrereleaseAsync(ct)
                : await GetLatestStableAsync(ct);

            // Релизов ещё нет — считаем, что версия актуальна.
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return Build(UpdateStatus.UpToDate, current, null, null);

            var info = ToInfo(release);
            var remote = ParseVersion(release.TagName);

            // Обычное обновление вперёд.
            if (remote > current)
                return Build(UpdateStatus.UpdateAvailable, current, info, null);

            // Особый случай: установлена бета-сборка, но выбран СТАБИЛЬНЫЙ канал,
            // а последняя стабильная версия по числам не выше текущей
            // (например стоит 0.2.2-beta.1, а стабильная — 0.2.1 или 0.2.2).
            // Это не «актуально», а возможность вернуться на стабильную сборку.
            if (channel == UpdateChannel.Stable && CurrentIsPrerelease() && !info.IsPrerelease)
                return Build(UpdateStatus.RollbackAvailable, current, info, null);

            return Build(UpdateStatus.UpToDate, current, info, null);
        }
        catch (Exception ex)
        {
            return Build(UpdateStatus.Failed, current, null, ex.Message);
        }
    }

    // releases/latest возвращает последний НЕ-пре-релиз.
    private static async Task<GhRelease?> GetLatestStableAsync(CancellationToken ct)
    {
        var url = $"{ApiBase}/repos/{Owner}/{Repo}/releases/latest";
        using var resp = await Http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null; // релизов нет
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<GhRelease>(json);
    }

    // Для beta берём самый свежий релиз, включая пре-релизы.
    private static async Task<GhRelease?> GetLatestIncludingPrereleaseAsync(CancellationToken ct)
    {
        var url = $"{ApiBase}/repos/{Owner}/{Repo}/releases?per_page=30";
        using var resp = await Http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        var all = JsonSerializer.Deserialize<List<GhRelease>>(json) ?? new List<GhRelease>();
        return all
            .Where(r => !r.Draft && !string.IsNullOrWhiteSpace(r.TagName))
            .OrderByDescending(r => ParseVersion(r.TagName))
            .ThenByDescending(r => r.PublishedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private static UpdateInfo ToInfo(GhRelease r)
    {
        var asset = PickInstaller(r.Assets);
        return new UpdateInfo
        {
            Version = ParseVersion(r.TagName).ToString(),
            TagName = r.TagName ?? string.Empty,
            ReleaseName = string.IsNullOrWhiteSpace(r.Name) ? (r.TagName ?? string.Empty) : r.Name!,
            ReleaseNotes = r.Body ?? string.Empty,
            ReleaseUrl = r.HtmlUrl ?? string.Empty,
            DownloadUrl = asset?.DownloadUrl,
            AssetName = asset?.Name,
            AssetSize = asset?.Size ?? 0,
            Sha256 = ExtractSha256(r.Body, asset?.Name),
            IsPrerelease = r.Prerelease,
            PublishedAt = r.PublishedAt,
        };
    }

    // Выбираем файл ДЛЯ АВТО-ОБНОВЛЕНИЯ среди прикреплённых к релизу.
    // Важно: в релизе теперь два файла — пакет приложения (.zip) и установщик
    // SimryxSetup.exe. Авто-обновление должно брать ZIP-пакет, а SimryxSetup.exe
    // предназначен только для первичной установки новыми пользователями, поэтому
    // он явно исключается.
    private static GhAsset? PickInstaller(List<GhAsset>? assets)
    {
        if (assets is null || assets.Count == 0) return null;

        bool Ends(GhAsset a, string ext) =>
            (a.Name ?? string.Empty).EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        bool IsSetup(GhAsset a) =>
            (a.Name ?? string.Empty).Contains("SimryxSetup", StringComparison.OrdinalIgnoreCase);

        return assets.FirstOrDefault(a => !IsSetup(a) && Ends(a, ".zip"))
            ?? assets.FirstOrDefault(a => Ends(a, ".msixbundle"))
            ?? assets.FirstOrDefault(a => Ends(a, ".msix"))
            ?? assets.FirstOrDefault(a => !IsSetup(a) && Ends(a, ".exe"))
            ?? assets.FirstOrDefault(a => Ends(a, ".zip"));
    }

    /// <summary>
    /// Ищем SHA-256 установщика в теле релиза. Поддерживается формат sha256sum
    /// (строка вида "&lt;64-hex&gt;  имя_файла"); если имя файла указано — берём точное
    /// совпадение, иначе — первый найденный 64-символьный hex как запасной вариант.
    /// </summary>
    private static string? ExtractSha256(string? body, string? assetName)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        string? fallback = null;
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var m = Regex.Match(line, "(?<![A-Fa-f0-9])([A-Fa-f0-9]{64})(?![A-Fa-f0-9])");
            if (!m.Success) continue;

            var hash = m.Groups[1].Value.ToLowerInvariant();
            if (!string.IsNullOrEmpty(assetName) &&
                line.Contains(assetName!, StringComparison.OrdinalIgnoreCase))
                return hash; // точное совпадение по имени файла

            fallback ??= hash;
        }
        return fallback;
    }

    /// <summary>"v0.2.0" / "0.2.0-beta.1" -> Version(0,2,0,0). Пре-релиз/билд-метаданные отбрасываются.</summary>
    public static Version ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return new Version(0, 0, 0, 0);
        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
        var cut = s.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) s = s.Substring(0, cut);
        var parts = s.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int Get(int i) => i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return new Version(Get(0), Get(1), Get(2), Get(3));
    }

    private static UpdateCheckResult Build(UpdateStatus status, Version current, UpdateInfo? info, string? error) =>
        new()
        {
            Status = status,
            CurrentVersion = current,
            Info = info,
            Error = error,
        };

    // ===== DTO ответа GitHub =====
    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<GhAsset>? Assets { get; set; }
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("content_type")] public string? ContentType { get; set; }
    }
}