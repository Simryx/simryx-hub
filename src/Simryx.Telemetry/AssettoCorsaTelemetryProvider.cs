using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Simryx.Telemetry;

/// <summary>
/// Провайдер телеметрии Assetto Corsa через shared memory (только чтение, anti-cheat-safe).
/// Читает три региона: acpmf_physics, acpmf_graphics, acpmf_static.
///
/// Раскладка структур — по фактически публикуемой этой сборкой AC (legacy SPageFileGraphic:
/// carCoordinates[3], одна машина). Поля поздних версий (свет, поворотники, дворники, slipRatio,
/// waterTemp, износ колодок, вибрации) этой сборкой в память не пишутся; в раскладке они
/// сокращены до минимума, нужного для корректного смещения читаемых полей.
/// </summary>
public sealed class AssettoCorsaTelemetryProvider : ITelemetryProvider
{
    public const string GameIdConst = "assetto_corsa";
    public string GameId => GameIdConst;

    private MemoryMappedFile? _physMmf;
    private MemoryMappedFile? _graphMmf;
    private MemoryMappedFile? _statMmf;
    private MemoryMappedViewAccessor? _phys;
    private MemoryMappedViewAccessor? _graph;
    private MemoryMappedViewAccessor? _stat;

    public bool IsConnected { get; private set; }

    public bool TryConnect()
    {
        try
        {
            _physMmf = MemoryMappedFile.OpenExisting("Local\\acpmf_physics", MemoryMappedFileRights.Read);
            _graphMmf = MemoryMappedFile.OpenExisting("Local\\acpmf_graphics", MemoryMappedFileRights.Read);
            _statMmf = MemoryMappedFile.OpenExisting("Local\\acpmf_static", MemoryMappedFileRights.Read);
            // size = 0 → маппим весь регион целиком; читаем потом не больше, чем реально есть.
            _phys = _physMmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            _graph = _graphMmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            _stat = _statMmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            IsConnected = true;
            return true;
        }
        catch
        {
            Cleanup();
            return false;
        }
    }

    public bool TryRead(out TelemetrySnapshot snapshot)
    {
        snapshot = new TelemetrySnapshot { GameId = GameIdConst };
        if (_phys is null || _graph is null || _stat is null) return false;

        try
        {
            var p = ReadStruct<AcPhysics>(_phys);
            var g = ReadStruct<AcGraphics>(_graph);
            var s = ReadStruct<AcStatic>(_stat);

            snapshot.Status = (TelemetryStatus)g.Status;

            // Управление
            snapshot.Throttle = p.Gas;
            snapshot.Brake = p.Brake;
            snapshot.Clutch = 1f - p.Clutch;   // AC: 1.0 = педаль отпущена
            snapshot.SteerAngle = p.SteerAngle;

            // Двигатель / трансмиссия
            snapshot.Rpm = p.Rpms;
            snapshot.MaxRpm = s.MaxRpm;
            snapshot.Gear = p.Gear - 1;        // 0=R,1=N,2=1st → -1/0/1
            snapshot.SpeedKmh = p.SpeedKmh;
            snapshot.TurboBoost = p.TurboBoost;
            snapshot.MaxTurboBoost = s.MaxTurboBoost;
            snapshot.PerformanceMeter = p.PerformanceMeter;
            snapshot.EngineBrake = p.EngineBrake;
            snapshot.AutoShifterOn = p.AutoShifterOn != 0;
            snapshot.MaxPower = s.MaxPower;
            snapshot.MaxTorque = s.MaxTorque;

            // Топливо
            snapshot.Fuel = p.Fuel;
            snapshot.MaxFuel = s.MaxFuel;
            snapshot.FuelPerLapGame = g.FuelXLap;
            snapshot.UsedFuelLastLap = g.UsedFuel;
            snapshot.FuelEstimatedLaps = g.FuelEstimatedLaps;

            // Системы помощи
            snapshot.Abs = p.Abs;
            snapshot.Tc = p.Tc;
            snapshot.DrsOpen = p.Drs > 0.5f;
            snapshot.DrsAvailable = p.DrsAvailable != 0;
            snapshot.DrsEnabled = p.DrsEnabled != 0;
            snapshot.PitLimiterOn = p.PitLimiterOn != 0;
            snapshot.HasDrs = s.HasDRS != 0;
            snapshot.HasErs = s.HasERS != 0;
            snapshot.HasKers = s.HasKERS != 0;

            // Включённые в игре ассисты
            snapshot.AidStability = s.AidStability;
            snapshot.AidAutoClutch = s.AidAutoClutch != 0;
            snapshot.AidAutoBlip = s.AidAutoBlip != 0;
            snapshot.AidTyreWearRate = s.AidTireRate;
            snapshot.AidFuelRate = s.AidFuelRate;
            snapshot.AidMechDamage = s.AidMechanicalDamage;
            snapshot.AidTyreBlankets = s.AidAllowTyreBlankets > 0.5f;

            // ERS / KERS
            snapshot.KersCharge = p.KersCharge;
            snapshot.KersInput = p.KersInput;
            snapshot.KersCurrentKj = p.KersCurrentKJ;
            snapshot.ErsRecoveryLevel = p.ErsRecoveryLevel;
            snapshot.ErsPowerLevel = p.ErsPowerLevel;
            snapshot.ErsIsCharging = p.ErsIsCharging != 0;
            snapshot.ErsIsHeatCharging = p.ErsHeatCharging != 0;

            // Перегрузки / динамика
            // Примечание: индексы accG — [0]=поперечная, [1]=вертикальная, [2]=продольная.
            snapshot.GForceLateral = Get(p.AccG, 0);
            snapshot.GForceVertical = Get(p.AccG, 1);
            snapshot.GForceLongitudinal = Get(p.AccG, 2);
            snapshot.Heading = p.Heading;
            snapshot.Pitch = p.Pitch;
            snapshot.Roll = p.Roll;
            snapshot.CgHeight = p.CgHeight;
            snapshot.NumberOfTyresOut = p.NumberOfTyresOut;
            snapshot.Velocity = p.Velocity ?? Array.Empty<float>();
            snapshot.LocalVelocity = p.LocalVelocity ?? Array.Empty<float>();
            snapshot.LocalAngularVel = p.LocalAngularVel ?? Array.Empty<float>();

            // Шасси / сетап
            snapshot.RideHeight = p.RideHeight ?? Array.Empty<float>();
            snapshot.SuspensionMaxTravel = s.SuspensionMaxTravel ?? Array.Empty<float>();
            snapshot.Ballast = p.Ballast;
            snapshot.AirDensity = p.AirDensity;
            snapshot.IsAiControlled = p.IsAiControlled != 0;

            // Тормоза
            snapshot.BrakeBias = p.BrakeBias;
            snapshot.BrakeTemp = p.BrakeTemp ?? Array.Empty<float>();

            // Шины
            snapshot.TyreCoreTemp = p.TyreCoreTemperature ?? Array.Empty<float>();
            snapshot.TyreTempInner = p.TyreTempI ?? Array.Empty<float>();
            snapshot.TyreTempMiddle = p.TyreTempM ?? Array.Empty<float>();
            snapshot.TyreTempOuter = p.TyreTempO ?? Array.Empty<float>();
            snapshot.TyrePressure = p.WheelsPressure ?? Array.Empty<float>();
            snapshot.TyreWear = p.TyreWear ?? Array.Empty<float>();
            snapshot.TyreDirt = p.TyreDirtyLevel ?? Array.Empty<float>();
            snapshot.Camber = p.CamberRad ?? Array.Empty<float>();
            snapshot.SuspensionTravel = p.SuspensionTravel ?? Array.Empty<float>();
            snapshot.WheelLoad = p.WheelLoad ?? Array.Empty<float>();
            snapshot.WheelSlip = p.WheelSlip ?? Array.Empty<float>();
            snapshot.WheelAngularSpeed = p.WheelAngularSpeed ?? Array.Empty<float>();
            snapshot.TyreRadius = s.TyreRadius ?? Array.Empty<float>();
            snapshot.TyreCompound = g.TyreCompound ?? "";

            // Повреждения / FFB
            snapshot.CarDamage = p.CarDamage ?? Array.Empty<float>();
            snapshot.FinalFf = p.FinalFF;

            // Окружение
            snapshot.AirTemp = p.AirTemp;
            snapshot.RoadTemp = p.RoadTemp;
            snapshot.SurfaceGrip = g.SurfaceGrip;
            snapshot.WindSpeed = g.WindSpeed;
            snapshot.WindDirection = g.WindDirection;

            // Тайминг / сессия
            snapshot.Position = g.Position;
            snapshot.CompletedLaps = g.CompletedLaps;
            snapshot.TotalLaps = g.NumberOfLaps;
            snapshot.CurrentSector = g.CurrentSectorIndex;
            snapshot.CurrentTimeMs = g.ICurrentTime;
            snapshot.LastTimeMs = g.ILastTime;
            snapshot.BestTimeMs = g.IBestTime;
            snapshot.LastSectorTimeMs = g.LastSectorTime;
            snapshot.SessionTimeLeftMs = g.SessionTimeLeft;
            snapshot.CurrentLapText = g.CurrentTime ?? "";
            snapshot.LastLapText = g.LastTime ?? "";
            snapshot.BestLapText = g.BestTime ?? "";
            snapshot.SplitText = g.Split ?? "";
            snapshot.DeltaToBestMs = g.IDeltaLapTime;
            snapshot.IsDeltaPositive = g.IsDeltaPositive != 0;
            snapshot.EstimatedLapMs = g.IEstimatedLapTime;
            snapshot.IsValidLap = g.IsValidLap != 0;

            snapshot.SessionType = g.Session;
            snapshot.Flag = g.Flag;
            snapshot.PenaltyTimeSec = g.PenaltyTime;
            snapshot.IdealLineOn = g.IdealLineOn != 0;
            snapshot.MandatoryPitDone = g.MandatoryPitDone != 0;
            snapshot.IsInPit = g.IsInPit != 0;
            snapshot.IsInPitLane = g.IsInPitLane != 0;
            snapshot.PitWindowStart = s.PitWindowStart;
            snapshot.PitWindowEnd = s.PitWindowEnd;

            // Мета
            snapshot.SectorCount = s.SectorCount;
            snapshot.TrackSplineLength = s.TrackSplineLength;
            snapshot.PenaltiesEnabled = s.PenaltiesEnabled != 0;
            snapshot.IsOnline = s.IsOnline != 0;

            // Прочее
            snapshot.CarModel = s.CarModel ?? "";
            snapshot.Track = s.Track ?? "";
            snapshot.TrackConfiguration = s.TrackConfiguration ?? "";
            snapshot.PlayerName = (s.PlayerName ?? "").Trim();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float Get(float[]? a, int i) => (a is not null && a.Length > i) ? a[i] : 0f;

    private static T ReadStruct<T>(MemoryMappedViewAccessor accessor) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        // Читаем не больше, чем реально есть в регионе; недостающий хвост остаётся нулём.
        int toRead = (int)Math.Min(size, accessor.Capacity);
        var bytes = new byte[size];
        accessor.ReadArray(0, bytes, 0, toRead);

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose() => Cleanup();

    private void Cleanup()
    {
        IsConnected = false;
        _phys?.Dispose(); _graph?.Dispose(); _stat?.Dispose();
        _physMmf?.Dispose(); _graphMmf?.Dispose(); _statMmf?.Dispose();
        _phys = _graph = _stat = null;
        _physMmf = _graphMmf = _statMmf = null;
    }

    // ===== Раскладка shared memory Assetto Corsa (Kunos SDK) =====

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    private struct AcPhysics
    {
        public int PacketId;
        public float Gas;
        public float Brake;
        public float Fuel;
        public int Gear;
        public int Rpms;
        public float SteerAngle;
        public float SpeedKmh;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Velocity;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] AccG;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelSlip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelLoad;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelsPressure;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelAngularSpeed;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreWear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreDirtyLevel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreCoreTemperature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] CamberRad;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] SuspensionTravel;
        public float Drs;
        public float Tc;
        public float Heading;
        public float Pitch;
        public float Roll;
        public float CgHeight;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public float[] CarDamage;
        public int NumberOfTyresOut;
        public int PitLimiterOn;
        public float Abs;
        public float KersCharge;
        public float KersInput;
        public int AutoShifterOn;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] RideHeight;
        public float TurboBoost;
        public float Ballast;
        public float AirDensity;
        public float AirTemp;
        public float RoadTemp;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] LocalAngularVel;
        public float FinalFF;
        public float PerformanceMeter;
        public int EngineBrake;
        public int ErsRecoveryLevel;
        public int ErsPowerLevel;
        public int ErsHeatCharging;
        public int ErsIsCharging;
        public float KersCurrentKJ;
        public int DrsAvailable;
        public int DrsEnabled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] BrakeTemp;
        public float Clutch;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempI;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempM;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempO;
        public int IsAiControlled;
        // Точки контакта шин не используются, но нужны для корректного смещения BrakeBias/LocalVelocity.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public float[] TyreContactPoint;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public float[] TyreContactNormal;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public float[] TyreContactHeading;
        public float BrakeBias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] LocalVelocity;
        // Дальше шёл расширенный хвост (slipRatio/waterTemp/износ колодок/вибрации) —
        // этой сборкой AC он не публикуется, в чтении не участвует, поэтому удалён.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    private struct AcGraphics
    {
        public int PacketId;
        public int Status;
        public int Session;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string CurrentTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string LastTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string BestTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string Split;
        public int CompletedLaps;
        public int Position;
        public int ICurrentTime;
        public int ILastTime;
        public int IBestTime;
        public float SessionTimeLeft;
        public float DistanceTraveled;
        public int IsInPit;
        public int CurrentSectorIndex;
        public int LastSectorTime;
        public int NumberOfLaps;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string TyreCompound;
        public float ReplayTimeMultiplier;
        public float NormalizedCarPosition;
        // Legacy: одна машина (X,Y,Z). Подтверждено сырым дампом (surfaceGrip@280).
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] CarCoordinates;
        public float PenaltyTime;
        public int Flag;
        public int IdealLineOn;
        public int IsInPitLane;
        public float SurfaceGrip;
        public int MandatoryPitDone;
        public float WindSpeed;
        public float WindDirection;
        // ===== Поля ниже этой сборкой AC не публикуются. Большинство не используется и
        // оставлено только как «отступ» до читаемых FuelXLap / IDeltaLapTime / IsValidLap / FuelEstimatedLaps. =====
        public int IsSetupMenuVisible;
        public int MainDisplayIndex;
        public int SecondaryDisplayIndex;
        public int GraphicsTC;
        public int GraphicsTCCut;
        public int EngineMap;
        public int GraphicsABS;
        public float FuelXLap;            // читается
        public int RainLights;
        public int FlashingLights;
        public int LightsStage;
        public float ExhaustTemperature;
        public int WiperLV;
        public int DriverStintTotalTimeLeft;
        public int DriverStintTimeLeft;
        public int RainTyres;
        public int SessionIndex;
        public float UsedFuel;            // читается
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string DeltaLapTime;
        public int IDeltaLapTime;         // читается
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string EstimatedLapTime;
        public int IEstimatedLapTime;     // читается
        public int IsDeltaPositive;       // читается
        public int ISplit;
        public int IsValidLap;            // читается
        public float FuelEstimatedLaps;   // читается — последнее нужное поле
        // Хвост (TrackStatus/Clock/DirectionLights…) не читается и удалён.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    private struct AcStatic
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string SmVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string AcVersion;
        public int NumberOfSessions;
        public int NumCars;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string CarModel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string Track;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerSurname;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerNick;
        public int SectorCount;
        public float MaxTorque;
        public float MaxPower;
        public int MaxRpm;
        public float MaxFuel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] SuspensionMaxTravel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreRadius;
        public float MaxTurboBoost;
        public float Deprecated1;
        public float Deprecated2;
        public int PenaltiesEnabled;
        public float AidFuelRate;
        public float AidTireRate;
        public float AidMechanicalDamage;
        public float AidAllowTyreBlankets;
        public float AidStability;
        public int AidAutoClutch;
        public int AidAutoBlip;
        public int HasDRS;
        public int HasERS;
        public int HasKERS;
        public float KersMaxJ;
        public int EngineBrakeSettingsCount;
        public int ErsPowerControllerCount;
        public float TrackSplineLength;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string TrackConfiguration;
        public float ErsMaxJ;
        public int IsTimedRace;
        public int HasExtraLap;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string CarSkin;
        public int ReversedGridPositions;
        public int PitWindowStart;
        public int PitWindowEnd;
        public int IsOnline;
    }
}