using Microsoft.Extensions.DependencyInjection;

namespace Simryx.App.Services;

/// <summary>
/// Глобальное состояние «уменьшить анимации». Значение читается из настроек
/// при первом обращении и кэшируется; страница настроек обновляет его вживую.
/// Анимационный код проверяет Reduced и пропускает анимации появления.
/// </summary>
public static class MotionService
{
    private const string SettingsKey = "ReduceMotion";
    private static bool? _reduced;

    public static bool Reduced
    {
        get
        {
            _reduced ??= App.Services.GetRequiredService<ILocalSettingsService>()
                            .Read<bool?>(SettingsKey) ?? false;
            return _reduced.Value;
        }
        set => _reduced = value;
    }
}