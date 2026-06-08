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
    // Стоим на пред-релизе (beta/rc), выбран стабильный канал, а последняя
    // стабильная версия по числам не выше текущей → можно вернуться на стабильную.
    RollbackAvailable,
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
    public string? Sha256 { get; init; }                       // контрольная сумма установщика (если опубликована в заметках)
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

    // Можно установить (как обновление вперёд, так и возврат на стабильную).
    public bool CanInstall =>
        Info is not null &&
        (Status == UpdateStatus.UpdateAvailable || Status == UpdateStatus.RollbackAvailable);
}

// ===== Часть 3: загрузка и установка =====

/// <summary>Прогресс загрузки установщика.</summary>
public sealed class UpdateDownloadProgress
{
    public long BytesReceived { get; init; }
    public long TotalBytes { get; init; }
    public double Fraction => TotalBytes > 0 ? Math.Clamp((double)BytesReceived / TotalBytes, 0, 1) : 0;
    public int Percent => (int)Math.Round(Fraction * 100);
}

/// <summary>Итог загрузки установщика.</summary>
public enum UpdateDownloadStatus
{
    Completed,
    IntegrityFailed,
    Failed,
    Canceled,
}

/// <summary>Результат загрузки: статус + путь к файлу + ошибка.</summary>
public sealed class UpdateDownloadResult
{
    public UpdateDownloadStatus Status { get; init; }
    public string? FilePath { get; init; }
    public string? Error { get; init; }
    public bool Success => Status == UpdateDownloadStatus.Completed && !string.IsNullOrEmpty(FilePath);
}