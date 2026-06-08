using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Simryx.Setup.Services;

public sealed class ReleaseInfo
{
    public string Version { get; init; } = "";
    public string TagName { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string AssetName { get; init; } = "";
    public long Size { get; init; }
}

/// <summary>Чтение последнего стабильного релиза и скачивание пакета приложения.</summary>
public sealed class GitHubClient
{
    private const string ApiBase = "https://api.github.com";
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("SimryxSetup");
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        h.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return h;
    }

    public async Task<ReleaseInfo> GetLatestStableAsync(CancellationToken ct = default)
    {
        var url = $"{ApiBase}/repos/{AppInfo.Owner}/{AppInfo.Repo}/releases/latest";
        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        var r = JsonSerializer.Deserialize<GhRelease>(json)
                ?? throw new Exception("Не удалось прочитать ответ GitHub.");

        var asset = PickPayload(r.Assets);
        if (asset is null)
            throw new Exception("В последнем стабильном релизе нет пакета обновления (.zip).");

        return new ReleaseInfo
        {
            Version = (r.TagName ?? "").TrimStart('v', 'V'),
            TagName = r.TagName ?? "",
            DownloadUrl = asset.DownloadUrl ?? "",
            AssetName = asset.Name ?? "",
            Size = asset.Size,
        };
    }

    public async Task DownloadAsync(string url, string destPath,
        IProgress<double> progress, CancellationToken ct = default)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress.Report(read * 100.0 / total);
        }
    }

    // Берём именно пакет приложения (.zip), а не сам установщик SimryxSetup.exe.
    private static GhAsset? PickPayload(List<GhAsset>? assets)
    {
        if (assets is null || assets.Count == 0) return null;
        bool IsSetup(GhAsset a) => (a.Name ?? "").Contains("SimryxSetup", StringComparison.OrdinalIgnoreCase);
        return assets.FirstOrDefault(a => !IsSetup(a) &&
                   (a.Name ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("assets")] public List<GhAsset>? Assets { get; set; }
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}