using Simryx.App.Services;
using Simryx.Telemetry;

namespace Simryx.App.Views.Telemetry;

/// <summary>
/// Экран телеметрии конкретной игры. TelemetryPage подставляет нужную
/// реализацию в зависимости от обнаруженной игры.
/// </summary>
public interface ITelemetryGameView
{
    /// <summary>Идентификатор игры из GameCatalog (например, "assetto_corsa").</summary>
    string GameId { get; }

    /// <summary>Передаёт сервис единиц измерения (метрические/имперские).</summary>
    void SetUnits(UnitsService? units);

    /// <summary>Обновляет показания по новому снимку телеметрии.</summary>
    void Render(TelemetrySnapshot snapshot);
}