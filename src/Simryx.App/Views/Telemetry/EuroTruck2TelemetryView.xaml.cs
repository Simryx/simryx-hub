using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simryx.App.Models;
using Simryx.App.Services;
using Simryx.Telemetry;
using Windows.UI;

namespace Simryx.App.Views.Telemetry;

public sealed partial class EuroTruck2TelemetryView : UserControl, ITelemetryGameView
{
    private readonly string _gameId;
    private readonly bool _en;
    private UnitsService? _units;
    private TelemetrySnapshot? _last;

    private static readonly Color CCaution = Rgb(242, 176, 74);
    private static readonly Color CCritical = Rgb(236, 91, 87);

    public EuroTruck2TelemetryView() : this("ets2") { }

    public EuroTruck2TelemetryView(string gameId)
    {
        InitializeComponent();
        _gameId = gameId;
        var lang = App.Services.GetService<ILocalSettingsService>()?.Read<string>("AppLanguage") ?? "ru-RU";
        _en = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        Loaded += (_, _) => LocalizeLabels();
}

    public string GameId => _gameId;

    public void SetUnits(UnitsService? units) => _units = units;

    public void Render(TelemetrySnapshot s)
    {
        _last = s;
        var t = s.Truck;
        if (t is null) return;

        bool imp = _units?.IsImperial ?? false;
        string speedUnit = imp ? "mph" : (_en ? "km/h" : "км/ч");
        string l = _en ? "L" : "л";

        // --- Грузовик ---
        TruckInfo.Text = JoinName(t.TruckBrand, t.TruckName);
        TruckLicense.Text = string.IsNullOrWhiteSpace(t.TruckLicense) ? "—" : t.TruckLicense;
        GameValue.Text = GameCatalog.FindById(s.GameId)?.Name ?? (t.GameId == 2 ? "American Truck Simulator" : "Euro Truck Simulator 2");
        GameTimeValue.Text = GameClock(t.TimeMinutes);
        PausedValue.Text = t.Paused ? (_en ? "paused" : "пауза") : (_en ? "running" : "идёт");

        // --- Двигатель и скорость ---
        double kmh = Math.Abs(t.SpeedMs) * 3.6;
        double sp = _units?.Speed((float)kmh) ?? kmh;
        SpeedValue.Text = $"{(int)Math.Round(sp)} {speedUnit}";
        RpmValue.Text = t.EngineRpmMax > 0
            ? $"{(int)Math.Round(t.EngineRpm)} / {(int)Math.Round(t.EngineRpmMax)}"
            : $"{(int)Math.Round(t.EngineRpm)}";
        GearValue.Text = GearLabel(t.DisplayedGear);
        CruiseValue.Text = t.CruiseOn
            ? $"{(int)Math.Round(_units?.Speed(t.CruiseControlMs * 3.6f) ?? t.CruiseControlMs * 3.6)} {speedUnit}"
            : (_en ? "off" : "выкл");
        IgnitionValue.Text = t.EngineEnabled
            ? (_en ? "engine on" : "двигатель")
            : t.ElectricEnabled ? (_en ? "ignition" : "зажигание") : (_en ? "off" : "выкл");
        RetarderValue.Text = t.RetarderLevel > 0 ? $"{t.RetarderLevel}/{t.RetarderStepCount}" : (_en ? "off" : "выкл");
        ParkingBrakeValue.Text = OnOff(t.ParkingBrake);
        DiffLockValue.Text = OnOff(t.DifferentialLock);

        // --- Управление ---
        SteerValue.Text = $"{t.UserSteering:0.00}";
        ThrottleVal.Text = $"{Math.Clamp(t.EffThrottle, 0f, 1f) * 100:0}%";
        BrakeVal.Text = $"{Math.Clamp(t.EffBrake, 0f, 1f) * 100:0}%";
        ClutchVal.Text = $"{Math.Clamp(t.EffClutch, 0f, 1f) * 100:0}%";
        HshifterValue.Text = t.HshifterSlot.ToString();

        // --- Топливо / AdBlue ---
        FuelValue.Text = $"{t.Fuel:0.0} {l}  ·  {Pct(Frac(t.Fuel, t.FuelCapacity))}";
        FuelConsumptionValue.Text = t.FuelAvgConsumption > 0
            ? $"{t.FuelAvgConsumption * 100:0.0} {l}/100{(_en ? "km" : "км")}"
            : "—";
        FuelRangeValue.Text = t.FuelRange > 0 ? DistText(t.FuelRange) : "—";
        AdBlueValue.Text = t.AdBlueCapacity > 0
            ? $"{t.AdBlue:0.0} {l}  ·  {Pct(Frac(t.AdBlue, t.AdBlueCapacity))}"
            : $"{t.AdBlue:0.0} {l}";

        // --- Температуры и давления ---
        OilTempValue.Text = TempStr(t.OilTemperature);
        OilPressValue.Text = PressStr(t.OilPressure);
        WaterTempValue.Text = TempStr(t.WaterTemperature);
        BatteryValue.Text = $"{t.BatteryVoltage:0.0} {(_en ? "V" : "В")}";
        AirPressValue.Text = PressStr(t.BrakeAirPressure);
        BrakeTempValue.Text = TempStr(t.BrakeTemperature);
        RenderWarnings(t);

        // --- Износ ---
        SetWear(WearEngineValue, t.WearEngine);
        SetWear(WearTransValue, t.WearTransmission);
        SetWear(WearCabinValue, t.WearCabin);
        SetWear(WearChassisValue, t.WearChassis);
        SetWear(WearWheelsValue, t.WearWheels);
        if (t.TrailerConnected)
        {
            SetWear(TrailerWearChassisValue, t.TrailerWearChassis);
            SetWear(TrailerWearWheelsValue, t.TrailerWearWheels);
            SetWear(CargoDamageValue, t.TrailerCargoDamage);
        }
        else
        {
            TrailerWearChassisValue.Text = "—";
            TrailerWearWheelsValue.Text = "—";
            CargoDamageValue.Text = "—";
        }

        // --- Свет и сигналы ---
        RenderLights(t);
        BlinkersValue.Text = $"{(t.BlinkerLeftActive ? "◀" : "–")}   {(t.BlinkerRightActive ? "▶" : "–")}";
        AuxValue.Text = $"{t.LightAuxFront} / {t.LightAuxRoof}";
        WipersValue.Text = OnOff(t.Wipers);

        // --- Груз и маршрут ---
        CargoValue.Text = string.IsNullOrWhiteSpace(t.CargoName) ? "—" : t.CargoName;
        CargoMassValue.Text = MassStr(t.CargoMass);
        TrailerValue.Text = t.TrailerConnected
            ? (string.IsNullOrWhiteSpace(t.TrailerName)
                ? (_en ? "connected" : "подключён")
                : t.TrailerName + (string.IsNullOrWhiteSpace(t.TrailerBodyType) ? "" : $" ({t.TrailerBodyType})"))
            : (_en ? "not connected" : "не подключён");
        RouteValue.Text = JoinArrow(t.SourceCity, t.DestinationCity);
        CompanyValue.Text = JoinArrow(t.SourceCompany, t.DestinationCompany);
        PlannedDistValue.Text = t.JobPlannedDistanceKm > 0 ? DistText(t.JobPlannedDistanceKm) : "—";
        IncomeValue.Text = t.JobIncome > 0 ? $"{t.JobIncome:N0}" : "—";
        JobStatusValue.Text = JobStatus(t);

        // --- Навигация ---
        NavDistValue.Text = t.NavDistance > 0 ? DistText(t.NavDistance / 1000.0) : "—";
        NavTimeValue.Text = NavTimeStr(t.NavTime);
        SpeedLimitValue.Text = t.NavSpeedLimit > 0
            ? $"{(int)Math.Round(_units?.Speed(t.NavSpeedLimit * 3.6f) ?? t.NavSpeedLimit * 3.6)} {speedUnit}"
            : "—";
        OdometerValue.Text = DistText(t.Odometer);
    }

    private void RenderWarnings(TruckData t)
    {
        var w = new List<string>();
        if (t.FuelWarning) w.Add(_en ? "fuel" : "топливо");
        if (t.AdBlueWarning) w.Add("AdBlue");
        if (t.OilPressureWarning) w.Add(_en ? "oil" : "масло");
        if (t.WaterTemperatureWarning) w.Add(_en ? "coolant" : "охл.");
        if (t.BatteryVoltageWarning) w.Add(_en ? "battery" : "АКБ");
        if (t.AirPressureWarning) w.Add(_en ? "air" : "воздух");
        if (t.AirPressureEmergency) w.Add(_en ? "AIR!" : "ВОЗДУХ!");

        if (w.Count == 0)
        {
            WarningsValue.Text = _en ? "none" : "нет";
            WarningsValue.Foreground = Brush("TextFillColorPrimaryBrush");
        }
        else
        {
            WarningsValue.Text = string.Join(" · ", w);
            WarningsValue.Foreground = new SolidColorBrush(CCritical);
        }
    }

    private void RenderLights(TruckData t)
    {
        var li = new List<string>();
        if (t.LightParking) li.Add(_en ? "park" : "габариты");
        if (t.LightLowBeam) li.Add(_en ? "low" : "ближний");
        if (t.LightHighBeam) li.Add(_en ? "high" : "дальний");
        if (t.LightBeacon) li.Add(_en ? "beacon" : "маячок");
        LightsValue.Text = li.Count == 0 ? (_en ? "off" : "выкл") : string.Join(" · ", li);
    }

    private string JobStatus(TruckData t)
    {
        var js = new List<string>();
        if (t.SpecialJob) js.Add(_en ? "special" : "спецгруз");
        js.Add(t.JobCargoLoaded ? (_en ? "loaded" : "загружен") : (_en ? "empty" : "пусто"));
        return string.Join(" · ", js);
    }

    private void SetWear(TextBlock tb, float frac)
    {
        tb.Text = Pct(frac);
        tb.Foreground = frac < 0.20f
            ? Brush("TextFillColorPrimaryBrush")
            : frac < 0.60f ? new SolidColorBrush(CCaution) : new SolidColorBrush(CCritical);
    }

    private string TempStr(float c) => $"{Math.Round(_units?.Temperature(c) ?? c):0}°";

    private string PressStr(float psi)
    {
        double kpa = psi * 6.894757;
        double v = _units?.Pressure(kpa) ?? kpa;
        string unit = (_units?.IsImperial ?? false) ? "psi" : (_en ? "kPa" : "кПа");
        return $"{v:0.0} {unit}";
    }

    private string DistText(double km)
    {
        if (_units?.IsImperial ?? false) return $"{km * 0.621371:0.0} {(_en ? "mi" : "ми")}";
        return $"{km:0.0} {(_en ? "km" : "км")}";
    }

    private string MassStr(float kg)
    {
        if (kg <= 0) return "—";
        return kg >= 1000f ? $"{kg / 1000f:0.0} {(_en ? "t" : "т")}" : $"{kg:0} {(_en ? "kg" : "кг")}";
    }

    private string NavTimeStr(float sec)
    {
        if (sec <= 0) return "—";
        var ts = TimeSpan.FromSeconds(sec);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}{(_en ? "h" : "ч")} {ts.Minutes:00}{(_en ? "m" : "м")}"
            : $"{ts.Minutes}{(_en ? "m" : "м")}";
    }

    private string OnOff(bool v) => v ? (_en ? "On" : "Вкл") : (_en ? "Off" : "Выкл");

    private static string Pct(float frac) => $"{Math.Clamp(frac, 0f, 1f) * 100:0}%";

    private static float Frac(float value, float max) => max > 0f ? value / max : 0f;

    private string JoinName(string brand, string name)
    {
        string s = $"{brand} {name}".Trim();
        return string.IsNullOrWhiteSpace(s) ? "—" : s;
    }

    private string JoinArrow(string a, string b)
    {
        bool ea = string.IsNullOrWhiteSpace(a);
        bool eb = string.IsNullOrWhiteSpace(b);
        if (ea && eb) return "—";
        return $"{(ea ? "—" : a)} → {(eb ? "—" : b)}";
    }

    private static string GameClock(uint minutes)
    {
        uint tod = minutes % 1440u;
        return $"{tod / 60:00}:{tod % 60:00}";
    }

    private static string GearLabel(int g) => g < 0 ? (g == -1 ? "R" : $"R{-g}") : g == 0 ? "N" : g.ToString();

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    private Brush Brush(string key)
        => Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(Colors.Gray);
// ---- Локализация статичных подписей (RU в XAML; EN подменяем при английском) ----
private static readonly Dictionary<string, string> _labels = new()
{
    // Заголовки секций
    { "ГРУЗОВИК", "TRUCK" },
    { "ДВИГАТЕЛЬ И СКОРОСТЬ", "ENGINE & SPEED" },
    { "УПРАВЛЕНИЕ", "CONTROLS" },
    { "ТОПЛИВО / ADBLUE", "FUEL / ADBLUE" },
    { "ТЕМПЕРАТУРЫ И ДАВЛЕНИЯ", "TEMPERATURES & PRESSURES" },
    { "ИЗНОС", "WEAR" },
    { "СВЕТ И СИГНАЛЫ", "LIGHTS & SIGNALS" },
    { "ГРУЗ И МАРШРУТ", "CARGO & ROUTE" },
    { "НАВИГАЦИЯ", "NAVIGATION" },
    // Подписи строк
    { "Модель", "Model" },
    { "Госномер", "Plate" },
    { "Игра", "Game" },
    { "Игровое время", "Game time" },
    { "Пауза", "Paused" },
    { "Скорость", "Speed" },
    { "Обороты / макс.", "RPM / max" },
    { "Передача", "Gear" },
    { "Круиз-контроль", "Cruise control" },
    { "Зажигание / двигатель", "Ignition / engine" },
    { "Ретардер", "Retarder" },
    { "Стояночный тормоз", "Parking brake" },
    { "Блокировка дифф.", "Diff lock" },
    { "Руль", "Steering" },
    { "Газ", "Throttle" },
    { "Тормоз", "Brake" },
    { "Сцепление", "Clutch" },
    { "Кулиса (слот)", "Shifter (slot)" },
    { "Топливо", "Fuel" },
    { "Расход (сред.)", "Consumption (avg.)" },
    { "Запас хода", "Range" },
    { "Масло (темп.)", "Oil (temp.)" },
    { "Масло (давл.)", "Oil (press.)" },
    { "Охл. жидкость", "Coolant" },
    { "АКБ", "Battery" },
    { "Тормозной воздух", "Brake air" },
    { "Тормоза (темп.)", "Brakes (temp.)" },
    { "Предупреждения", "Warnings" },
    { "Двигатель", "Engine" },
    { "Трансмиссия", "Transmission" },
    { "Кабина", "Cabin" },
    { "Шасси", "Chassis" },
    { "Колёса", "Wheels" },
    { "Прицеп (шасси)", "Trailer (chassis)" },
    { "Прицеп (колёса)", "Trailer (wheels)" },
    { "Повреждение груза", "Cargo damage" },
    { "Фары", "Lights" },
    { "Поворотники", "Turn signals" },
    { "Доп. свет (перед/крыша)", "Aux lights (front/roof)" },
    { "Дворники", "Wipers" },
    { "Груз", "Cargo" },
    { "Масса", "Mass" },
    { "Прицеп", "Trailer" },
    { "Маршрут", "Route" },
    { "Компании", "Companies" },
    { "Плановая дистанция", "Planned distance" },
    { "Доход", "Income" },
    { "Статус работы", "Job status" },
    { "До цели", "To destination" },
    { "Время в пути", "Time to arrival" },
    { "Ограничение", "Speed limit" },
    { "Одометр", "Odometer" },
};

private void LocalizeLabels()
{
    if (!_en) return;
    foreach (var tb in Descendants<TextBlock>(this))
    {
        var key = Normalize(tb.Text);
        if (key.Length > 0 && _labels.TryGetValue(key, out var en))
            tb.Text = en;
    }
}

private static string Normalize(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
    var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parts);
}

private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
{
    int count = VisualTreeHelper.GetChildrenCount(root);
    for (int i = 0; i < count; i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T typed) yield return typed;
        foreach (var d in Descendants<T>(child)) yield return d;
    }
}
}