namespace Simryx.Telemetry;

// Грузовые данные ETS2/ATS для снимка телеметрии.
public sealed class TruckData
{
    public int GameId { get; set; }          // 1 = ETS2, 2 = ATS
    public bool Paused { get; set; }
    public uint TimeMinutes { get; set; }
    public string GameVersion { get; set; } = string.Empty;

    // движение / двигатель
    public float SpeedMs { get; set; }        // знаковый
    public float EngineRpm { get; set; }
    public float EngineRpmMax { get; set; }

    // ввод
    public float UserSteering { get; set; }
    public float UserThrottle { get; set; }
    public float UserBrake { get; set; }
    public float UserClutch { get; set; }
    public float EffSteering { get; set; }
    public float EffThrottle { get; set; }
    public float EffBrake { get; set; }
    public float EffClutch { get; set; }

    // круиз
    public float CruiseControlMs { get; set; }
    public bool CruiseOn { get; set; }

    // топливо / AdBlue
    public float Fuel { get; set; }
    public float FuelCapacity { get; set; }
    public float FuelAvgConsumption { get; set; }
    public float FuelRange { get; set; }
    public float AdBlue { get; set; }
    public float AdBlueCapacity { get; set; }

    // двигатель/тормоза/электрика
    public float OilPressure { get; set; }
    public float OilTemperature { get; set; }
    public float WaterTemperature { get; set; }
    public float BatteryVoltage { get; set; }
    public float BrakeAirPressure { get; set; }
    public float BrakeTemperature { get; set; }
    public float DashboardBacklight { get; set; }
    public float Odometer { get; set; }

    // износ
    public float WearEngine { get; set; }
    public float WearTransmission { get; set; }
    public float WearCabin { get; set; }
    public float WearChassis { get; set; }
    public float WearWheels { get; set; }

    // навигация
    public float NavDistance { get; set; }
    public float NavTime { get; set; }
    public float NavSpeedLimit { get; set; }

    // КПП
    public int EngineGear { get; set; }
    public int DisplayedGear { get; set; }
    public uint ForwardGearCount { get; set; }
    public uint ReverseGearCount { get; set; }
    public uint HshifterSlot { get; set; }
    public uint RetarderLevel { get; set; }
    public uint RetarderStepCount { get; set; }
    public uint LightAuxFront { get; set; }
    public uint LightAuxRoof { get; set; }

    // флаги
    public bool ElectricEnabled { get; set; }
    public bool EngineEnabled { get; set; }
    public bool ParkingBrake { get; set; }
    public bool MotorBrake { get; set; }
    public bool DifferentialLock { get; set; }
    public bool LiftAxle { get; set; }
    public bool LiftAxleIndicator { get; set; }
    public bool TrailerLiftAxle { get; set; }
    public bool FuelWarning { get; set; }
    public bool AdBlueWarning { get; set; }
    public bool OilPressureWarning { get; set; }
    public bool WaterTemperatureWarning { get; set; }
    public bool BatteryVoltageWarning { get; set; }
    public bool AirPressureWarning { get; set; }
    public bool AirPressureEmergency { get; set; }
    public bool BlinkerLeftActive { get; set; }
    public bool BlinkerRightActive { get; set; }
    public bool BlinkerLeftOn { get; set; }
    public bool BlinkerRightOn { get; set; }
    public bool LightParking { get; set; }
    public bool LightLowBeam { get; set; }
    public bool LightHighBeam { get; set; }
    public bool LightBeacon { get; set; }
    public bool LightBrake { get; set; }
    public bool LightReverse { get; set; }
    public bool Wipers { get; set; }
    public bool TrailerConnected { get; set; }
    public bool JobCargoLoaded { get; set; }
    public bool SpecialJob { get; set; }

    // прицеп
    public float TrailerWearChassis { get; set; }
    public float TrailerWearWheels { get; set; }
    public float TrailerCargoDamage { get; set; }

    // работа
    public float JobPlannedDistanceKm { get; set; }
    public float CargoMass { get; set; }
    public ulong JobIncome { get; set; }
    public uint JobDeliveryTime { get; set; }

    // мировое положение
    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public double WorldZ { get; set; }
    public float WorldHeading { get; set; }
    public float WorldPitch { get; set; }
    public float WorldRoll { get; set; }

    // строки
    public string TruckBrand { get; set; } = string.Empty;
    public string TruckName { get; set; } = string.Empty;
    public string TruckLicense { get; set; } = string.Empty;
    public string TrailerName { get; set; } = string.Empty;
    public string TrailerBodyType { get; set; } = string.Empty;
    public string CargoId { get; set; } = string.Empty;
    public string CargoName { get; set; } = string.Empty;
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string SourceCompany { get; set; } = string.Empty;
    public string DestinationCompany { get; set; } = string.Empty;
}