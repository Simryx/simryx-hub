using System;
using System.Threading;
using System.Threading.Tasks;

namespace Simryx.App.Services;

/// <summary>
/// Координатор фоновой авто-проверки обновлений.
/// Выполняет тихую проверку ОДИН раз за сессию приложения (при запуске),
/// если включена настройка AutoCheckUpdates. Результат кэшируется и
/// публикуется через событие UpdateFound, чтобы Главная страница
/// могла показать баннер «Доступно обновление».
/// </summary>
public static class UpdateCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ranThisSession;

    /// <summary>Последний результат, в котором найдено обновление (если был).</summary>
    public static UpdateCheckResult? LastAvailable { get; private set; }

    /// <summary>Срабатывает, когда тихая проверка нашла доступное обновление.</summary>
    public static event EventHandler<UpdateCheckResult>? UpdateFound;

    /// <summary>
    /// Тихая проверка при запуске. Безопасно вызывать несколько раз —
    /// фактическая проверка выполнится только один раз за сессию.
    /// Наружу не бросает исключений.
    /// </summary>
    public static async Task RunStartupCheckAsync(CancellationToken ct = default)
    {
        if (_ranThisSession) return;

        await Gate.WaitAsync(ct);
        try
        {
            if (_ranThisSession) return;
            _ranThisSession = true;

            // Уважаем настройку: авто-проверка выключена — выходим тихо.
            if (!ReadAutoCheckEnabled()) return;

            var channel = ReadChannel();
            var result = await new UpdateService().CheckForUpdatesAsync(channel, ct);

            // Тихо: показываем что-либо ТОЛЬКО когда реально есть обновление.
            if (result.HasUpdate)
            {
                LastAvailable = result;
                UpdateFound?.Invoke(null, result);
            }
        }
        catch
        {
            // Тихая проверка не должна мешать запуску — гасим любые ошибки.
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Сброс состояния сессии: следующая RunStartupCheckAsync снова выполнит
    /// проверку, а устаревший кэш найденного обновления очищается.
    /// Вызывается, например, при смене канала обновлений в «Параметрах».
    /// </summary>
    public static void ResetSession()
    {
        _ranThisSession = false;
        LastAvailable = null;
    }

    private static bool ReadAutoCheckEnabled()
    {
        try
        {
            var settings = App.Services?.GetService(typeof(ILocalSettingsService)) as ILocalSettingsService;
            // По умолчанию авто-проверка включена.
            return settings?.Read<bool?>("AutoCheckUpdates") ?? true;
        }
        catch
        {
            return true;
        }
    }

    private static UpdateChannel ReadChannel()
    {
        try
        {
            var settings = App.Services?.GetService(typeof(ILocalSettingsService)) as ILocalSettingsService;
            var raw = settings?.Read<string>("UpdateChannel");
            if (!string.IsNullOrWhiteSpace(raw) &&
                raw.Contains("beta", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateChannel.Beta;
            }
        }
        catch
        {
            // ignore — по умолчанию стабильный канал
        }
        return UpdateChannel.Stable;
    }
}