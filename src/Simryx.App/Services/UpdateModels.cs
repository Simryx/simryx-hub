using System;

namespace Simryx.App.Services;

/// <summary>Канал обновлений.</summary>
public enum UpdateChannel
{
    Stable,
    Beta,
}

/// <summary>Итог проверки обновлений.</summary>
public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

/// <summary>Сведения о доступном релизе (из GitHub Releases).</summary>
public sealed class UpdateInfo
{
    public string Version { get; init; } = string.Empty;       // нормализованная: "0.2.0"
    public string TagName { get; init; } = string.Empty;       // тег: "v0.2.0"
    public string ReleaseName { get; init; } = string.Empty;   // заголовок релиза
    public string ReleaseNotes { get; init; } = string.Empty;  // тело релиза (Markdown)
    public string ReleaseUrl { get; init; } = string.Empty;    // ссылка на страницу релиза
    public string? DownloadUrl { get; init; }                  // прямая ссылка на установщик
    public string? AssetName { get; init; }                    // имя файла установщика
    public long AssetSize { get; init; }                       // размер, байт
    public bool IsPrerelease { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
}

/// <summary>Результат проверки: статус + сведения о релизе + текущая версия.</summary>
public sealed class UpdateCheckResult
{
    public UpdateStatus Status { get; init; }
    public UpdateInfo? Info { get; init; }
    public Version CurrentVersion { get; init; } = new(0, 0, 0, 0);
    public string? Error { get; init; }

    public bool HasUpdate => Status == UpdateStatus.UpdateAvailable && Info is not null;
}