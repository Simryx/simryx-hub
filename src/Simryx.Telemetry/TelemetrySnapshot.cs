using System;

namespace Simryx.Telemetry;

public enum TelemetryStatus { Off = 0, Replay = 1, Live = 2, Pause = 3 }

/// <summary>
/// Полный снимок телеметрии. Базовые единицы — метрические (км/ч, °C, кПа* / у AC давление в psi).
/// Массивы колёс: индексы 0=FL, 1=FR, 2=RL, 3=RR.
/// </summary>
public sealed class TelemetrySnapshot
{
    public string GameId { get; set; } = "";
    public TelemetryStatus Status { get; set; }

    // ===== Управление =====
    public float Throttle { get; set; }
    public float Brake { get; set; }
    public float Clutch { get; set; }       // 1.0 = педаль выжата (уже инвертировано из AC)
    public float SteerAngle { get; set; }    // требует калибровки под устройство

    // ===== Двигатель / трансмиссия =====
    public int Rpm { get; set; }
    public int MaxRpm { get; set; }
    public int Gear { get; set; }            // -1=R, 0=N, 1.. = передачи
    public float SpeedKmh { get; set; }
    public float TurboBoost { get; set; }
    public float MaxTurboBoost { get; set; }
    public float PerformanceMeter { get; set; }
    public int EngineBrake { get; set; }
    public bool AutoShifterOn { get; set; }
    public float MaxPower { get; set; }
    public float MaxTorque { get; set; }

    // ===== Топливо =====
    public float Fuel { get; set; }          // литры
    public float MaxFuel { get; set; }       // литры
    public float FuelPercent => MaxFuel > 0 ? Math.Clamp(Fuel / MaxFuel, 0f, 1f) : 0f;
    public float FuelPerLapGame { get; set; }    // расход на круг от движка (л), 0 = нет данных
    public float UsedFuelLastLap { get; set; }   // израсходовано за прошлый круг (л)
    public float FuelEstimatedLaps { get; set; } // на сколько кругов хватит (оценка движка)

    // ===== Системы помощи =====
    public float Abs { get; set; }
    public float Tc { get; set; }
    public bool AbsActive => Abs > 0.05f;
    public bool TcActive => Tc > 0.05f;
    public bool DrsOpen { get; set; }
    public bool DrsAvailable { get; set; }
    public bool DrsEnabled { get; set; }
    public bool PitLimiterOn { get; set; }
    public bool HasDrs { get; set; }
    public bool HasErs { get; set; }
    public bool HasKers { get; set; }

    // ===== Включённые в игре ассисты (из static) =====
    public float AidStability { get; set; }     // 0..1
    public bool AidAutoClutch { get; set; }
    public bool AidAutoBlip { get; set; }
    public float AidTyreWearRate { get; set; }
    public float AidFuelRate { get; set; }
    public float AidMechDamage { get; set; }
    public bool AidTyreBlankets { get; set; }

    // ===== ERS / KERS =====
    public float KersCharge { get; set; }
    public float KersInput { get; set; }
    public float KersCurrentKj { get; set; }
    public int ErsRecoveryLevel { get; set; }
    public int ErsPowerLevel { get; set; }
    public bool ErsIsCharging { get; set; }
    public bool ErsIsHeatCharging { get; set; }

    // ===== Перегрузки / динамика =====
    public float GForceLongitudinal { get; set; }
    public float GForceLateral { get; set; }
    public float GForceVertical { get; set; }
    public float Heading { get; set; }
    public float Pitch { get; set; }
    public float Roll { get; set; }
    public float CgHeight { get; set; }
    public int NumberOfTyresOut { get; set; }
    public float[] Velocity { get; set; } = Array.Empty<float>();
    public float[] LocalVelocity { get; set; } = Array.Empty<float>();
    public float[] LocalAngularVel { get; set; } = Array.Empty<float>();

    // ===== Шасси / сетап =====
    public float[] RideHeight { get; set; } = Array.Empty<float>();          // [перед, зад], м
    public float[] SuspensionMaxTravel { get; set; } = Array.Empty<float>(); // макс. ход (м), для расчёта %
    public float Ballast { get; set; }
    public float AirDensity { get; set; }
    public bool IsAiControlled { get; set; }

    // ===== Тормоза =====
    public float BrakeBias { get; set; }
    public float[] BrakeTemp { get; set; } = Array.Empty<float>();

    // ===== Шины (все индексы FL/FR/RL/RR) =====
    public float[] TyreCoreTemp { get; set; } = Array.Empty<float>();
    public float[] TyreTempInner { get; set; } = Array.Empty<float>();
    public float[] TyreTempMiddle { get; set; } = Array.Empty<float>();
    public float[] TyreTempOuter { get; set; } = Array.Empty<float>();
    public float[] TyrePressure { get; set; } = Array.Empty<float>();   // psi (как отдаёт AC)
    public float[] TyreWear { get; set; } = Array.Empty<float>();
    public float[] TyreDirt { get; set; } = Array.Empty<float>();
    public float[] Camber { get; set; } = Array.Empty<float>();
    public float[] SuspensionTravel { get; set; } = Array.Empty<float>();
    public float[] WheelLoad { get; set; } = Array.Empty<float>();
    public float[] WheelSlip { get; set; } = Array.Empty<float>();
    public float[] WheelAngularSpeed { get; set; } = Array.Empty<float>();
    public float[] TyreRadius { get; set; } = Array.Empty<float>();
    public string TyreCompound { get; set; } = "";

    // ===== Повреждения =====
    public float[] CarDamage { get; set; } = Array.Empty<float>(); // [перед,зад,лево,право,центр]

    // ===== Световые приборы (graphics, новые версии AC) =====
    public int LightsStage { get; set; }      // 0=выкл, 1=габариты, 2=фары
    public bool HighBeamFlash { get; set; }   // моргание дальним
    public bool TurnLeft { get; set; }        // левый поворотник
    public bool TurnRight { get; set; }       // правый поворотник
    public bool RainLights { get; set; }      // задний противотуманный
    public int WiperLevel { get; set; }       // 0=выкл, 1.. = режим дворников

    // ===== Вибрации (готовые каналы AC, задел под тактильные эффекты) =====
    public float KerbVibration { get; set; }  // поребрики
    public float SlipVibration { get; set; }  // пробуксовка
    public float AbsVibration { get; set; }   // срабатывание ABS
    public float GVibration { get; set; }     // перегрузки

    // ===== Тормоза / шины — расширенная физика =====
    public float[] BrakePressure { get; set; } = Array.Empty<float>(); // давление по колёсам, 0..1
    public float[] PadLife { get; set; } = Array.Empty<float>();       // ресурс колодок
    public float[] DiscLife { get; set; } = Array.Empty<float>();      // ресурс дисков
    public float[] SlipRatio { get; set; } = Array.Empty<float>();     // продольный slip по колёсам
    public float[] SlipAngle { get; set; } = Array.Empty<float>();     // угол увода по колёсам
    public float WaterTemp { get; set; }                               // темп. охлаждающей жидкости

    // ===== Force Feedback =====
    public float FinalFf { get; set; }

    // ===== Окружение =====
    public float AirTemp { get; set; }
    public float RoadTemp { get; set; }
    public float SurfaceGrip { get; set; }
    public float WindSpeed { get; set; }
    public float WindDirection { get; set; }

    // ===== Тайминг / сессия =====
    public int Position { get; set; }
    public int CompletedLaps { get; set; }
    public int TotalLaps { get; set; }
    public int CurrentSector { get; set; }
    public int CurrentTimeMs { get; set; }
    public int LastTimeMs { get; set; }
    public int BestTimeMs { get; set; }
    public int LastSectorTimeMs { get; set; }
    public float SessionTimeLeftMs { get; set; }
    public string CurrentLapText { get; set; } = "";
    public string LastLapText { get; set; } = "";
    public string BestLapText { get; set; } = "";
    public string SplitText { get; set; } = "";
    public int DeltaToBestMs { get; set; }    // готовая дельта от движка (мс), знак учитывает IsDeltaPositive
    public bool IsDeltaPositive { get; set; } // true = медленнее лучшего
    public int EstimatedLapMs { get; set; }   // прогноз времени текущего круга (мс)
    public bool IsValidLap { get; set; } = true;
    public float NormalizedCarPosition { get; set; }
    public float DistanceTraveled { get; set; }
    public int SessionType { get; set; }     // 0=unknown,1=practice,2=qualify,3=race,...
    public int Flag { get; set; }            // 0=none,1=blue,2=yellow,3=black,4=white,5=checkered,6=penalty
    public float PenaltyTimeSec { get; set; }
    public bool IdealLineOn { get; set; }
    public bool MandatoryPitDone { get; set; }
    public bool IsInPit { get; set; }
    public bool IsInPitLane { get; set; }
    public int PitWindowStart { get; set; }
    public int PitWindowEnd { get; set; }

    // ===== Позиции машин на трассе (для карты и гэпов) =====
    public int ActiveCars { get; set; }                          // сколько машин в сессии
    public int PlayerCarIndex { get; set; }                      // индекс игрока в CarPosX/CarPosZ
    public float[] CarPosX { get; set; } = Array.Empty<float>(); // мировая X каждой машины
    public float[] CarPosZ { get; set; } = Array.Empty<float>(); // мировая Z (плоскость земли)
    public float[] CarPosY = Array.Empty<float>();   // высота (временно, для диагностики оси)
    public string CarDebug = "";                      // ВРЕМЕННО: сырые координаты игрока
    public bool MapCalibrated;

    // ===== Мета (из static) =====
    public int SectorCount { get; set; }
    public float TrackSplineLength { get; set; }
    public bool PenaltiesEnabled { get; set; }
    public bool IsOnline { get; set; }

    // ===== Прочее =====
    public string CarModel { get; set; } = "";
    public string Track { get; set; } = "";
    public string TrackConfiguration { get; set; } = "";   // ← папка лейаута (для поиска карты)
    public string PlayerName { get; set; } = "";
    public bool IsDriving => Status == TelemetryStatus.Live && SpeedKmh > 1f;

    // Грузовые данные (ETS2/ATS). null для гоночных игр.
    public TruckData? Truck { get; set; }
}