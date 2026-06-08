using System;
using System.Globalization;

namespace Simryx.App.Services;

public enum UnitSystem { Metric, Imperial }

/// <summary>
/// Единый источник правды по единицам измерения. Читает настройку "Units"
/// и форматирует телеметрию (скорость, температуру, давление, дистанцию).
/// Вход всегда в метрических базовых единицах: км/ч, °C, кПа, км.
/// </summary>
public sealed class UnitsService
{
    private readonly ILocalSettingsService _settings;

    /// <summary>Срабатывает при смене системы единиц — живые экраны переформатируют значения.</summary>
    public event Action? Changed;

    public UnitsService(ILocalSettingsService settings) => _settings = settings;

    public UnitSystem System =>
        string.Equals(_settings.Read<string>("Units"), "Imperial", StringComparison.OrdinalIgnoreCase)
            ? UnitSystem.Imperial
            : UnitSystem.Metric;

    public bool IsImperial => System == UnitSystem.Imperial;

    /// <summary>Вызывать после смены настройки «Единицы», чтобы открытые экраны обновились.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    // ===== Подписи единиц (для заголовков и осей) =====

    public string SpeedUnit => IsImperial ? "mph" : "км/ч";
    public string TemperatureUnit => IsImperial ? "°F" : "°C";
    public string PressureUnit => IsImperial ? "psi" : "кПа";
    public string DistanceUnit => IsImperial ? "mi" : "км";

    // ===== Конвертация (вход — метрические базовые единицы) =====

    public double Speed(double kmh) => IsImperial ? kmh * 0.621371 : kmh;
    public double Temperature(double celsius) => IsImperial ? celsius * 9.0 / 5.0 + 32.0 : celsius;
    public double Pressure(double kPa) => IsImperial ? kPa * 0.1450377 : kPa;
    public double Distance(double km) => IsImperial ? km * 0.621371 : km;

    // ===== Готовые строки с подписью =====

    public string FormatSpeed(double kmh, int digits = 0) => Format(Speed(kmh), digits, SpeedUnit);
    public string FormatTemperature(double celsius, int digits = 0) => Format(Temperature(celsius), digits, TemperatureUnit);
    public string FormatPressure(double kPa, int digits = 0) => Format(Pressure(kPa), digits, PressureUnit);
    public string FormatDistance(double km, int digits = 1) => Format(Distance(km), digits, DistanceUnit);

    private static string Format(double value, int digits, string unit)
    {
        var number = Math.Round(value, digits).ToString("N" + digits, CultureInfo.CurrentCulture);
        return $"{number} {unit}";
    }
}