using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Simryx.App.Services;

/// <summary>
/// Тосты Windows через AppNotificationManager (входит в Windows App SDK, без сторонних пакетов).
/// Каждая категория уважает свой переключатель в настройках.
/// </summary>
public sealed class NotificationService
{
    private readonly ILocalSettingsService _settings;
    private bool _registered;

    public NotificationService(ILocalSettingsService settings) => _settings = settings;

    /// <summary>Регистрирует приложение в системе уведомлений (вызывать один раз при старте).</summary>
    public void Register()
    {
        if (_registered) return;
        AppNotificationManager.Default.Register();
        _registered = true;
    }

    /// <summary>Снимает регистрацию (вызывать при выходе).</summary>
    public void Unregister()
    {
        if (!_registered) return;
        try { AppNotificationManager.Default.Unregister(); } catch { /* не критично при выходе */ }
        _registered = false;
    }

    private bool Enabled(string key, bool def = true) => _settings.Read<bool?>(key) ?? def;

    // ===== 4 категории =====

    public void NotifyUpdate(string title, string message)
    {
        if (Enabled("NotifyUpdates")) Show(title, message);
    }

    public void NotifyDevice(string title, string message)
    {
        if (Enabled("NotifyDevices")) Show(title, message);
    }

    public void NotifyFirmware(string title, string message)
    {
        if (Enabled("NotifyFirmware")) Show(title, message);
    }

    public void NotifyError(string title, string message)
    {
        // Ошибки по умолчанию тоже включены, но уважаем переключатель.
        if (Enabled("NotifyErrors")) Show(title, message);
    }

    private static void Show(string title, string message)
    {
        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }
}