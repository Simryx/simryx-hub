using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Simryx.Telemetry;

/// <summary>
/// Провайдер телеметрии Euro Truck Simulator 2 / American Truck Simulator через
/// наш SCS-плагин (memory-mapped файл Local\SimryxTruckTelemetry). Только чтение, anti-cheat-safe.
/// Один и тот же MMF обслуживает обе игры; конкретная игра задаётся в конструкторе.
/// </summary>
public sealed class EuroTruck2TelemetryProvider : ITelemetryProvider
{
    public const string Ets2IdConst = "ets2";
    public const string AtsIdConst = "ats";

    private const string MmfName = "Local\\SimryxTruckTelemetry";
    private const uint ExpectedStructVersion = 1u;

    private readonly string _gameId;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;

    public EuroTruck2TelemetryProvider(string gameId) => _gameId = gameId;

    public string GameId => _gameId;
    public bool IsConnected { get; private set; }

    public bool TryConnect()
    {
        try
        {
            _mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);
            _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
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
        snapshot = new TelemetrySnapshot { GameId = _gameId };
        if (_view is null) return false;

        try
        {
            var d = ReadStruct<SimryxTruckShared>(_view);

            // Плагин жив и раскладка совместима?
            if (d.sdk_active == 0 || d.structure_version != ExpectedStructVersion)
                return false;

            bool paused = d.paused != 0;
            snapshot.Status = paused ? TelemetryStatus.Pause : TelemetryStatus.Live;

            // ---- базовые поля, совместимые с racing-снимком (для общих виджетов) ----
            snapshot.SpeedKmh = d.speed * 3.6f;      // знаковый: <0 = задний ход
            snapshot.Rpm = (int)MathF.Round(d.engine_rpm);
            snapshot.MaxRpm = (int)MathF.Round(d.engine_rpm_max);
            snapshot.Gear = d.displayed_gear;        // <0 R, 0 N, >0 передача
            snapshot.Throttle = d.eff_throttle;
            snapshot.Brake = d.eff_brake;
            snapshot.Clutch = d.eff_clutch;
            snapshot.SteerAngle = d.user_steering;   // -1..1
            snapshot.Fuel = d.fuel;
            snapshot.MaxFuel = d.fuel_capacity;

            // ---- полные грузовые данные ----
            snapshot.Truck = new TruckData
            {
                GameId = (int)d.game_id,
                Paused = paused,
                TimeMinutes = d.time_abs_minutes,
                GameVersion = SimryxTruckSharedExt.Str(d.game_version),

                SpeedMs = d.speed,
                EngineRpm = d.engine_rpm,
                EngineRpmMax = d.engine_rpm_max,

                UserSteering = d.user_steering,
                UserThrottle = d.user_throttle,
                UserBrake = d.user_brake,
                UserClutch = d.user_clutch,
                EffSteering = d.eff_steering,
                EffThrottle = d.eff_throttle,
                EffBrake = d.eff_brake,
                EffClutch = d.eff_clutch,

                CruiseControlMs = d.cruise_control,
                CruiseOn = d.cruise_on != 0,

                Fuel = d.fuel,
                FuelCapacity = d.fuel_capacity,
                FuelAvgConsumption = d.fuel_avg_consumption,
                FuelRange = d.fuel_range,
                AdBlue = d.adblue,
                AdBlueCapacity = d.adblue_capacity,

                OilPressure = d.oil_pressure,
                OilTemperature = d.oil_temperature,
                WaterTemperature = d.water_temperature,
                BatteryVoltage = d.battery_voltage,
                BrakeAirPressure = d.brake_air_pressure,
                BrakeTemperature = d.brake_temperature,
                DashboardBacklight = d.dashboard_backlight,
                Odometer = d.odometer,

                WearEngine = d.wear_engine,
                WearTransmission = d.wear_transmission,
                WearCabin = d.wear_cabin,
                WearChassis = d.wear_chassis,
                WearWheels = d.wear_wheels,

                NavDistance = d.nav_distance,
                NavTime = d.nav_time,
                NavSpeedLimit = d.nav_speed_limit,

                EngineGear = d.engine_gear,
                DisplayedGear = d.displayed_gear,
                ForwardGearCount = d.forward_gear_count,
                ReverseGearCount = d.reverse_gear_count,
                HshifterSlot = d.hshifter_slot,
                RetarderLevel = d.retarder_level,
                RetarderStepCount = d.retarder_step_count,
                LightAuxFront = d.light_aux_front,
                LightAuxRoof = d.light_aux_roof,

                ElectricEnabled = d.electric_enabled != 0,
                EngineEnabled = d.engine_enabled != 0,
                ParkingBrake = d.parking_brake != 0,
                MotorBrake = d.motor_brake != 0,
                DifferentialLock = d.differential_lock != 0,
                LiftAxle = d.lift_axle != 0,
                LiftAxleIndicator = d.lift_axle_indicator != 0,
                TrailerLiftAxle = d.trailer_lift_axle != 0,
                FuelWarning = d.fuel_warning != 0,
                AdBlueWarning = d.adblue_warning != 0,
                OilPressureWarning = d.oil_pressure_warning != 0,
                WaterTemperatureWarning = d.water_temperature_warning != 0,
                BatteryVoltageWarning = d.battery_voltage_warning != 0,
                AirPressureWarning = d.air_pressure_warning != 0,
                AirPressureEmergency = d.air_pressure_emergency != 0,
                BlinkerLeftActive = d.blinker_left_active != 0,
                BlinkerRightActive = d.blinker_right_active != 0,
                BlinkerLeftOn = d.blinker_left_on != 0,
                BlinkerRightOn = d.blinker_right_on != 0,
                LightParking = d.light_parking != 0,
                LightLowBeam = d.light_low_beam != 0,
                LightHighBeam = d.light_high_beam != 0,
                LightBeacon = d.light_beacon != 0,
                LightBrake = d.light_brake != 0,
                LightReverse = d.light_reverse != 0,
                Wipers = d.wipers != 0,
                TrailerConnected = d.trailer_connected != 0,
                JobCargoLoaded = d.job_cargo_loaded != 0,
                SpecialJob = d.special_job != 0,

                TrailerWearChassis = d.trailer_wear_chassis,
                TrailerWearWheels = d.trailer_wear_wheels,
                TrailerCargoDamage = d.trailer_cargo_damage,

                JobPlannedDistanceKm = d.job_planned_distance_km,
                CargoMass = d.cargo_mass,
                JobIncome = d.job_income,
                JobDeliveryTime = d.job_delivery_time,

                WorldX = d.world_x,
                WorldY = d.world_y,
                WorldZ = d.world_z,
                WorldHeading = d.world_heading,
                WorldPitch = d.world_pitch,
                WorldRoll = d.world_roll,

                TruckBrand = SimryxTruckSharedExt.Str(d.truck_brand),
                TruckName = SimryxTruckSharedExt.Str(d.truck_name),
                TruckLicense = SimryxTruckSharedExt.Str(d.truck_license),
                TrailerName = SimryxTruckSharedExt.Str(d.trailer_name),
                TrailerBodyType = SimryxTruckSharedExt.Str(d.trailer_body_type),
                CargoId = SimryxTruckSharedExt.Str(d.cargo_id),
                CargoName = SimryxTruckSharedExt.Str(d.cargo_name),
                SourceCity = SimryxTruckSharedExt.Str(d.source_city),
                DestinationCity = SimryxTruckSharedExt.Str(d.destination_city),
                SourceCompany = SimryxTruckSharedExt.Str(d.source_company),
                DestinationCompany = SimryxTruckSharedExt.Str(d.destination_company),
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static T ReadStruct<T>(MemoryMappedViewAccessor accessor) where T : struct
    {
        int size = Marshal.SizeOf<T>();
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
        _view?.Dispose();
        _mmf?.Dispose();
        _view = null;
        _mmf = null;
    }
}