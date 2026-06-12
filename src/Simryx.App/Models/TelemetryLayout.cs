using System;

namespace Simryx.App.Models;

/// <summary>Логические секции вкладки телеметрии. Каждая игра показывает свой набор.</summary>
public enum TelemetrySection
{
    Status,
    Core,
    Pedals,
    GForce,
    LapsFuel,
    DeltaPredict,
    Tyres,
    Chassis,
    Brakes,
    Engine,
    Assists,
    AidsInGame,
    SessionTrack,
    Damage,
}

/// <summary>
/// Какие секции (и в каком порядке) показывать для конкретной игры. По мере
/// добавления новых игр сюда добавляется их раскладка; страница телеметрии
/// просто показывает/скрывает соответствующие карточки.
/// </summary>
public static class TelemetryLayouts
{
    public static readonly TelemetrySection[] AssettoCorsa =
    {
        TelemetrySection.Status,
        TelemetrySection.Core,
        TelemetrySection.Pedals,
        TelemetrySection.GForce,
        TelemetrySection.LapsFuel,
        TelemetrySection.DeltaPredict,
        TelemetrySection.Tyres,
        TelemetrySection.Chassis,
        TelemetrySection.Brakes,
        TelemetrySection.Engine,
        TelemetrySection.Assists,
        TelemetrySection.AidsInGame,
        TelemetrySection.SessionTrack,
        TelemetrySection.Damage,
    };

    // Безопасный минимум для игр, которые ещё детально не размечены.
    public static readonly TelemetrySection[] Generic =
    {
        TelemetrySection.Status,
        TelemetrySection.Core,
        TelemetrySection.Pedals,
        TelemetrySection.LapsFuel,
        TelemetrySection.Tyres,
    };

    public static TelemetrySection[] For(string gameId) => gameId switch
    {
        "assetto_corsa" => AssettoCorsa,
        _ => Generic,
    };

    public static bool Has(string gameId, TelemetrySection section)
        => Array.IndexOf(For(gameId), section) >= 0;
}