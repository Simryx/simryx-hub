using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simryx.App.Models;
using Simryx.App.Services;
using Simryx.Telemetry;
using Windows.UI;
using System.Collections.Generic;

namespace Simryx.App.Views.Telemetry;

public sealed partial class AssettoCorsaTelemetryView : UserControl, ITelemetryGameView
{
    private UnitsService? _units;
    private TelemetrySnapshot? _last;
    private readonly bool _en;

    // Визуальный ход руля в каждую сторону (град.). 450 ≈ 900° полного хода — дорожный авто.
    private const double SteerLockDeg = 450;

    private static readonly Color CSuccess = Rgb(47, 191, 158);
    private static readonly Color CCaution = Rgb(242, 176, 74);
    private static readonly Color CAccent = Rgb(90, 182, 232);
    private static readonly Color CCritical = Rgb(236, 91, 87);

    // Расчёт расхода топлива по кругам.
    private int _lastLap = -1;
    private float _fuelAtLapStart = -1f;
    private float _fuelPerLap;

    public AssettoCorsaTelemetryView()
    {
        InitializeComponent();
        var lang = App.Services.GetService<ILocalSettingsService>()?.Read<string>("AppLanguage") ?? "ru-RU";
        _en = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
	    Loaded += (_, _) => LocalizeLabels();
    }

    public string GameId => "assetto_corsa";

    public void SetUnits(UnitsService? units) => _units = units;

    public void Render(TelemetrySnapshot s)
    {
        _last = s;

        bool imp = _units?.IsImperial ?? false;
        string speedUnit = imp ? "mph" : "км/ч";
        string pressUnit = imp ? "psi" : "кПа";
        string l = _en ? "L" : "л";

        // Статус.
        StatusCar.Text = string.IsNullOrWhiteSpace(s.CarModel)
            ? (GameCatalog.FindById(s.GameId)?.Name ?? "—") : s.CarModel;
        StatusTrack.Text = string.IsNullOrWhiteSpace(s.Track) ? "—" : s.Track;
        StatusSession.Text = SessionName(s.SessionType);
        StatusPos.Text = s.Position > 0 ? $"P{s.Position}" : "—";
        StatusLap.Text = s.TotalLaps > 0 ? $"{s.CompletedLaps + 1}/{s.TotalLaps}" : $"{s.CompletedLaps + 1}";
        StatusAir.Text = TempStr(s.AirTemp);
        StatusRoad.Text = TempStr(s.RoadTemp);
        StatusGrip.Text = $"{s.SurfaceGrip * 100:0}%";
        ApplyFlag(s.Flag);

        // Управление (руль + педали + передача).
        double steerDeg = s.SteerAngle * SteerLockDeg;
        SteerValue.Text = $"{steerDeg:0}°  ({s.SteerAngle:0.00})";
        ThrottleVal.Text = $"{Math.Clamp(s.Throttle, 0f, 1f) * 100:0}%";
        BrakeVal.Text = $"{Math.Clamp(s.Brake, 0f, 1f) * 100:0}%";
        ClutchVal.Text = $"{Math.Clamp(s.Clutch, 0f, 1f) * 100:0}%";
        GearValue.Text = GearLabel(s.Gear);

        // Двигатель и скорость.
        double speed = _units?.Speed(s.SpeedKmh) ?? s.SpeedKmh;
        SpeedValue.Text = $"{(int)Math.Round(speed)} {speedUnit}";
        RpmValue.Text = s.MaxRpm > 0 ? $"{s.Rpm} / {s.MaxRpm}" : s.Rpm.ToString();

        // Перегрузки.
        GLongValue.Text = $"{s.GForceLongitudinal:0.00}";
        GLatValue.Text = $"{s.GForceLateral:0.00}";
        GVertValue.Text = $"{s.GForceVertical:0.00}";

        // Тайминг.
        LapCurrentText.Text = FormatLap(s.CurrentTimeMs);
        LapLastText.Text = FormatLap(s.LastTimeMs);
        LapBestText.Text = FormatLap(s.BestTimeMs);
        DeltaText.Text = string.IsNullOrWhiteSpace(s.SplitText) ? "—" : s.SplitText;
        SectorText.Text = $"S{s.CurrentSector + 1}";
        SessionLeftText.Text = FormatClock(s.SessionTimeLeftMs);

        // Топливо (расчёт по кругам).
        UpdateFuel(s);

        // Дельта / прогноз / топливо (от движка игры).
        DeltaGameText.Text = DeltaStr(s.DeltaToBestMs, s.IsDeltaPositive);
        EstimatedLapText.Text = s.EstimatedLapMs > 0 ? FormatLap(s.EstimatedLapMs) : "—";
        ValidLapText.Text = YesNo(s.IsValidLap);
        FuelPerLapGameText.Text = s.FuelPerLapGame > 0 ? $"{s.FuelPerLapGame:0.00} {l}" : "—";
        UsedFuelText.Text = s.UsedFuelLastLap > 0 ? $"{s.UsedFuelLastLap:0.00} {l}" : "—";
        FuelLapsGameText.Text = s.FuelEstimatedLaps > 0 ? $"{s.FuelEstimatedLaps:0.0}" : "—";

        // Шины.
        TyreFL.Text = TyreStr(s, 0, pressUnit);
        TyreFR.Text = TyreStr(s, 1, pressUnit);
        TyreRL.Text = TyreStr(s, 2, pressUnit);
        TyreRR.Text = TyreStr(s, 3, pressUnit);

        // Шасси / сетап.
        RideHeightVal.Text = s.RideHeight.Length >= 2
            ? $"{Get(s.RideHeight, 0) * 1000f:0} / {Get(s.RideHeight, 1) * 1000f:0} {(_en ? "mm" : "мм")}"
            : "—";
        SuspTravelVal.Text = SuspStr(s);
        BallastVal.Text = $"{s.Ballast:0} {(_en ? "kg" : "кг")}";
        AirDensityVal.Text = $"{s.AirDensity:0.000} {(_en ? "kg/m³" : "кг/м³")}";
        AiControlVal.Text = YesNo(s.IsAiControlled);

        // Тормоза.
        BrakeFL.Text = BrakeLine(s, 0);
        BrakeFR.Text = BrakeLine(s, 1);
        BrakeRL.Text = BrakeLine(s, 2);
        BrakeRR.Text = BrakeLine(s, 3);
        BrakeBiasText.Text = s.BrakeBias > 0 ? $"{s.BrakeBias * 100:0.0}%" : "—";

        // Электроника (состояние).
        AbsValue.Text = s.AbsActive ? (_en ? "ON" : "ВКЛ") : (_en ? "off" : "выкл");
        TcValue.Text = s.TcActive ? (_en ? "ON" : "ВКЛ") : (_en ? "off" : "выкл");
        DrsValue.Text = s.DrsOpen ? (_en ? "OPEN" : "ОТКР") : "—";
        PitLimiterValue.Text = s.PitLimiterOn ? (_en ? "ON" : "ВКЛ") : (_en ? "off" : "выкл");

        // Ассисты (настройки игры).
        AidStabilityVal.Text = $"{s.AidStability * 100:0}%";
        AidAutoClutchVal.Text = OnOff(s.AidAutoClutch);
        AidAutoBlipVal.Text = OnOff(s.AidAutoBlip);
        AidTyreRateVal.Text = $"{s.AidTyreWearRate:0.0}×";
        AidFuelRateVal.Text = $"{s.AidFuelRate:0.0}×";
        AidMechVal.Text = $"{s.AidMechDamage * 100:0}%";
        AidBlanketsVal.Text = OnOff(s.AidTyreBlankets);

        // Сессия / трасса.
        PenaltyTimeVal.Text = s.PenaltyTimeSec > 0 ? $"{s.PenaltyTimeSec:0.0} {(_en ? "s" : "с")}" : "—";
        IdealLineVal.Text = OnOff(s.IdealLineOn);
        PitWindowVal.Text = (s.PitWindowStart > 0 || s.PitWindowEnd > 0) ? $"{s.PitWindowStart}–{s.PitWindowEnd}" : "—";
        SectorCountVal.Text = s.SectorCount > 0 ? s.SectorCount.ToString() : "—";
        TrackLenVal.Text = s.TrackSplineLength > 0 ? $"{s.TrackSplineLength:0} {(_en ? "m" : "м")}" : "—";
        PenaltiesOnVal.Text = OnOff(s.PenaltiesEnabled);
        OnlineVal.Text = YesNo(s.IsOnline);

        // Повреждения.
        DamageFront.Text = $"{Get(s.CarDamage, 0):0}";
        DamageRear.Text = $"{Get(s.CarDamage, 1):0}";
        DamageLeft.Text = $"{Get(s.CarDamage, 2):0}";
        DamageRight.Text = $"{Get(s.CarDamage, 3):0}";
        DamageColor(DamageFront, Get(s.CarDamage, 0));
        DamageColor(DamageRear, Get(s.CarDamage, 1));
        DamageColor(DamageLeft, Get(s.CarDamage, 2));
        DamageColor(DamageRight, Get(s.CarDamage, 3));
    }

    private void UpdateFuel(TelemetrySnapshot s)
    {
        if (s.CompletedLaps != _lastLap)
        {
            if (_fuelAtLapStart > 0 && s.Fuel <= _fuelAtLapStart)
            {
                var used = _fuelAtLapStart - s.Fuel;
                if (used > 0.05f) _fuelPerLap = used;
            }
            _fuelAtLapStart = s.Fuel;
            _lastLap = s.CompletedLaps;
        }
        string l = _en ? "L" : "л";
        FuelText.Text = $"{s.Fuel:0.0} {l}  ·  {s.FuelPercent * 100:0}%";
        PerLapText.Text = _fuelPerLap > 0 ? $"{_fuelPerLap:0.0} {l}" : "—";
        RangeText.Text = _fuelPerLap > 0
            ? $"≈ {(int)Math.Floor(s.Fuel / _fuelPerLap)} {(_en ? "laps" : "кр.")}" : "—";
    }

    private string TyreStr(TelemetrySnapshot s, int w, string pressUnit)
    {
        float? core = ValueAt(s.TyreCoreTemp, w);
        float? press = ValueAt(s.TyrePressure, w);
        float? wear = ValueAt(s.TyreWear, w);
        float slip = ValueAt(s.WheelSlip, w) ?? 0f;
        string ct = core.HasValue ? $"{Math.Round(_units?.Temperature(core.Value) ?? core.Value):0}°" : "—";
        string pr = press.HasValue
            ? $"{(_units?.Pressure(press.Value * 6.894757) ?? press.Value * 6.894757):0.0} {pressUnit}" : "—";
        string wr = wear.HasValue ? $"{wear.Value:0.0}%" : "—";
        return $"{ct}  ·  {pr}  ·  {(_en ? "wear" : "износ")} {wr}  ·  {(_en ? "slip" : "пробукс.")} {slip:0.0}";
    }

    private string SuspStr(TelemetrySnapshot s)
    {
        string P(int i)
        {
            float max = Get(s.SuspensionMaxTravel, i);
            float cur = Get(s.SuspensionTravel, i);
            if (max <= 0f) return "—";
            return $"{Math.Clamp(cur / max * 100f, 0f, 100f):0}%";
        }
        return $"{P(0)} / {P(1)} / {P(2)} / {P(3)}";
    }

    private void ApplyFlag(int flag)
    {
        var (text, color) = flag switch
        {
            1 => (_en ? "BLUE" : "СИНИЙ", CAccent),
            2 => (_en ? "YELLOW" : "ЖЁЛТЫЙ", CCaution),
            3 => (_en ? "BLACK" : "ЧЁРНЫЙ", CCritical),
            4 => (_en ? "WHITE" : "БЕЛЫЙ", Rgb(220, 220, 220)),
            5 => (_en ? "FINISH" : "ФИНИШ", CSuccess),
            6 => (_en ? "PENALTY" : "ШТРАФ", CCritical),
            _ => (_en ? "CLEAR" : "ЧИСТО", Rgb(120, 130, 140)),
        };
        FlagText.Text = text;
        FlagText.Foreground = new SolidColorBrush(color);
    }

    private void DamageColor(TextBlock tb, double v)
        => tb.Foreground = v < 1 ? Brush("TextFillColorPrimaryBrush")
            : v < 30 ? new SolidColorBrush(CCaution) : new SolidColorBrush(CCritical);

    private static float? ValueAt(float[]? a, int i) => (a is not null && a.Length > i) ? a[i] : (float?)null;
    private static float Get(float[]? a, int i) => (a is not null && a.Length > i) ? a[i] : 0f;

    private string TempStr(float c) => $"{Math.Round(_units?.Temperature(c) ?? c):0}°";

    private string BrakeLine(TelemetrySnapshot s, int i)
    {
        float? temp = ValueAt(s.BrakeTemp, i);
        return temp.HasValue ? $"{Math.Round(_units?.Temperature(temp.Value) ?? temp.Value):0}°" : "—";
    }

    private string YesNo(bool v) => v ? (_en ? "Yes" : "Да") : (_en ? "No" : "Нет");
    private string OnOff(bool v) => v ? (_en ? "On" : "Вкл") : (_en ? "Off" : "Выкл");

    private static string DeltaStr(int ms, bool positive)
    {
        if (ms == 0) return "—";
        float sec = Math.Abs(ms) / 1000f;
        return $"{(positive ? "+" : "−")}{sec:0.000}";
    }

    private static string FormatLap(int ms)
    {
        if (ms <= 0 || ms > 3_600_000) return "—";
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds:000}";
    }

    private static string FormatClock(float ms)
    {
        if (ms <= 0 || ms > 86_400_000) return "—";
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    private string SessionName(int session) => session switch
    {
        1 => _en ? "Practice" : "Практика",
        2 => _en ? "Qualify" : "Квалификация",
        3 => _en ? "Race" : "Гонка",
        4 => _en ? "Hotlap" : "Хотлап",
        5 => _en ? "Time Attack" : "Тайм-атака",
        6 => _en ? "Drift" : "Дрифт",
        7 => _en ? "Drag" : "Драг",
        _ => "—",
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    private Brush Brush(string key)
        => Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(Colors.Gray);

    private static string GearLabel(int gear) => gear switch { < 0 => "R", 0 => "N", _ => gear.ToString() };
    // ---- Локализация статичных подписей (RU в XAML; EN подменяем при английском) ----
private static readonly Dictionary<string, string> _labels = new()
{
	// Заголовки секций
	{ "СТАТУС", "STATUS" },
	{ "УПРАВЛЕНИЕ", "CONTROLS" },
	{ "ДВИГАТЕЛЬ И СКОРОСТЬ", "ENGINE & SPEED" },
	{ "ПЕРЕГРУЗКИ (G)", "G-FORCES" },
	{ "ШИНЫ (темп · давление · износ · пробукс.)", "TYRES (temp · pressure · wear · slip)" },
	{ "ТОРМОЗА (темп.)", "BRAKES (temp.)" },
	{ "ШАССИ / СЕТАП", "CHASSIS / SETUP" },
	{ "КРУГИ И ТОПЛИВО", "LAPS & FUEL" },
	{ "ДЕЛЬТА / ПРОГНОЗ (от игры)", "DELTA / ESTIMATE (from game)" },
	{ "ЭЛЕКТРОНИКА (СОСТОЯНИЕ)", "ELECTRONICS (STATE)" },
	{ "АССИСТЫ (НАСТРОЙКИ ИГРЫ)", "ASSISTS (GAME SETTINGS)" },
	{ "СЕССИЯ / ТРАССА", "SESSION / TRACK" },
	{ "ПОВРЕЖДЕНИЯ", "DAMAGE" },
	// Подписи строк
	{ "Авто", "Car" },
	{ "Трасса", "Track" },
	{ "Сессия", "Session" },
	{ "Позиция", "Position" },
	{ "Круг", "Lap" },
	{ "Темп. воздуха", "Air temp." },
	{ "Темп. трассы", "Track temp." },
	{ "Зацеп трассы", "Track grip" },
	{ "Флаг", "Flag" },
	{ "Руль (угол)", "Steering (angle)" },
	{ "Газ", "Throttle" },
	{ "Тормоз", "Brake" },
	{ "Сцепление (педаль)", "Clutch (pedal)" },
	{ "Передача", "Gear" },
	{ "Скорость", "Speed" },
	{ "Обороты", "RPM" },
	{ "Наддув (турбо)", "Boost (turbo)" },
	{ "Усилие на руле (FFB)", "Wheel force (FFB)" },
	{ "Заряд ERS", "ERS charge" },
	{ "Рекуперация ERS", "ERS recovery" },
	{ "Колёс вне трассы", "Wheels off track" },
	{ "Продольная", "Longitudinal" },
	{ "Боковая", "Lateral" },
	{ "Вертикальная", "Vertical" },
	{ "Перед. левая", "Front left" },
	{ "Перед. правая", "Front right" },
	{ "Задн. левая", "Rear left" },
	{ "Задн. правая", "Rear right" },
	{ "Перед. левый", "Front left" },
	{ "Перед. правый", "Front right" },
	{ "Задн. левый", "Rear left" },
	{ "Задн. правый", "Rear right" },
	{ "Баланс тормозов", "Brake bias" },
	{ "Клиренс перед / зад", "Ride height F / R" },
	{ "Ход подвески (FL/FR/RL/RR)", "Susp. travel (FL/FR/RL/RR)" },
	{ "Балласт", "Ballast" },
	{ "Плотность воздуха", "Air density" },
	{ "Под управлением ИИ", "AI controlled" },
	{ "Текущий круг", "Current lap" },
	{ "Последний круг", "Last lap" },
	{ "Лучший круг", "Best lap" },
	{ "Сплит к лучшему", "Split to best" },
	{ "Сектор", "Sector" },
	{ "Осталось в сессии", "Session remaining" },
	{ "Топливо", "Fuel" },
	{ "Расход/круг (расчёт)", "Per lap (calc.)" },
	{ "Хватит на (расчёт)", "Range (calc.)" },
	{ "Дельта к лучшему", "Delta to best" },
	{ "Прогноз круга", "Estimated lap" },
	{ "Круг засчитан", "Lap valid" },
	{ "Расход/круг (игра)", "Per lap (game)" },
	{ "Израсходовано за круг", "Used last lap" },
	{ "Хватит на кругов (игра)", "Laps left (game)" },
	{ "ABS (антиблокировка)", "ABS (anti-lock)" },
	{ "TC (антипробуксовка)", "TC (traction)" },
	{ "DRS (антикрыло)", "DRS (rear wing)" },
	{ "Огранич. пит-лейн", "Pit limiter" },
	{ "Контроль устойчивости", "Stability control" },
	{ "Авто-сцепление", "Auto clutch" },
	{ "Авто-перегазовка", "Auto blip" },
	{ "Износ шин (множитель)", "Tyre wear (mult.)" },
	{ "Расход топлива (множитель)", "Fuel rate (mult.)" },
	{ "Механ. повреждения", "Mech. damage" },
	{ "Грелки шин", "Tyre blankets" },
	{ "Штрафное время", "Penalty time" },
	{ "Идеальная траектория", "Ideal line" },
	{ "Пит-окно (кругов)", "Pit window (laps)" },
	{ "Секторов", "Sectors" },
	{ "Длина трассы", "Track length" },
	{ "Штрафы включены", "Penalties on" },
	{ "Онлайн-сессия", "Online session" },
	{ "Перед", "Front" },
	{ "Зад", "Rear" },
	{ "Лево", "Left" },
	{ "Право", "Right" },
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