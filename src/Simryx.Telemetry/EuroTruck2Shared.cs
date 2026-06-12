using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Simryx.Telemetry;

// Точное зеркало simryx_truck_shared_t (simryx_shared.h, #pragma pack(4), 980 байт).
// ВАЖНО: порядок и типы полей менять только синхронно с .h в плагине.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct SimryxTruckShared
{
    // ----- заголовок -----
    public uint structure_version;
    public uint game_id;          // 1 = ETS2, 2 = ATS, 0 = неизвестно
    public uint time_abs_minutes;
    public uint paused;           // 0/1
    public uint sdk_active;       // 1 пока плагин жив

    // ----- грузовик: float -----
    public float speed;           // м/с (знак: <0 = задний ход)
    public float engine_rpm;
    public float engine_rpm_max;
    public float user_steering;
    public float user_throttle;
    public float user_brake;
    public float user_clutch;
    public float eff_steering;
    public float eff_throttle;
    public float eff_brake;
    public float eff_clutch;
    public float cruise_control;  // м/с
    public float fuel;            // л
    public float fuel_capacity;   // л
    public float fuel_avg_consumption; // л/км
    public float fuel_range;      // км
    public float adblue;          // л
    public float adblue_capacity; // л
    public float oil_pressure;    // psi
    public float oil_temperature; // °C
    public float water_temperature; // °C
    public float battery_voltage; // В
    public float brake_air_pressure; // psi
    public float brake_temperature;  // °C
    public float dashboard_backlight; // 0..1
    public float odometer;        // км
    public float wear_engine;     // 0..1
    public float wear_transmission;
    public float wear_cabin;
    public float wear_chassis;
    public float wear_wheels;
    public float nav_distance;    // м
    public float nav_time;        // с
    public float nav_speed_limit; // м/с

    // векторы (локальные)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] lin_vel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] ang_vel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] lin_acc;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] ang_acc;

    // мировое положение
    public double world_x;
    public double world_y;
    public double world_z;
    public float world_heading;   // 0..1
    public float world_pitch;
    public float world_roll;

    // ----- прицеп -----
    public float trailer_wear_chassis;
    public float trailer_wear_wheels;
    public float trailer_cargo_damage;

    // ----- работа -----
    public float job_planned_distance_km;
    public float cargo_mass;      // кг
    public ulong job_income;
    public uint job_delivery_time;

    // ----- int -----
    public int engine_gear;       // <0 = задняя
    public int displayed_gear;
    public uint forward_gear_count;
    public uint reverse_gear_count;
    public uint hshifter_slot;
    public uint retarder_level;
    public uint retarder_step_count;
    public uint light_aux_front;
    public uint light_aux_roof;

    // ----- флаги (uint8) -----
    public byte electric_enabled;
    public byte engine_enabled;
    public byte parking_brake;
    public byte motor_brake;
    public byte differential_lock;
    public byte lift_axle;
    public byte lift_axle_indicator;
    public byte trailer_lift_axle;
    public byte fuel_warning;
    public byte adblue_warning;
    public byte oil_pressure_warning;
    public byte water_temperature_warning;
    public byte battery_voltage_warning;
    public byte air_pressure_warning;
    public byte air_pressure_emergency;
    public byte blinker_left_active;
    public byte blinker_right_active;
    public byte blinker_left_on;
    public byte blinker_right_on;
    public byte light_parking;
    public byte light_low_beam;
    public byte light_high_beam;
    public byte light_beacon;
    public byte light_brake;
    public byte light_reverse;
    public byte wipers;
    public byte cruise_on;
    public byte trailer_connected;
    public byte job_cargo_loaded;
    public byte special_job;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] _pad_flags;

    // ----- строки (UTF-8, фикс. длина) -----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] truck_brand;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] truck_name;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] truck_license;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] trailer_name;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] trailer_body_type;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] cargo_id;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] cargo_name;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] source_city;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] destination_city;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] source_company;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] destination_company;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] game_version;
}

// Хелперы декодирования фиксированных буферов.
internal static class SimryxTruckSharedExt
{
    public static string Str(byte[]? buf)
    {
        if (buf is null || buf.Length == 0) return string.Empty;
        int n = Array.IndexOf(buf, (byte)0);
        if (n < 0) n = buf.Length;
        return n == 0 ? string.Empty : Encoding.UTF8.GetString(buf, 0, n);
    }

    public static float At(float[]? v, int i) => (v is not null && i < v.Length) ? v[i] : 0f;
}