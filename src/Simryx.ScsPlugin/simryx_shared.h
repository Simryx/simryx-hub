#pragma once
#include <cstdint>

// Имя memory-mapped файла. Тот же литерал использует C#-провайдер.
#define SIMRYX_TRUCK_MMF_NAME "Local\\SimryxTruckTelemetry"
#define SIMRYX_TRUCK_STRUCT_VERSION 1u

#pragma pack(push, 4)
struct simryx_truck_shared_t
{
    // ----- заголовок -----
    uint32_t structure_version; // = SIMRYX_TRUCK_STRUCT_VERSION
    uint32_t game_id;           // 1 = ETS2, 2 = ATS, 0 = неизвестно
    uint32_t time_abs_minutes;  // игровое время (минуты)
    uint32_t paused;            // 0/1
    uint32_t sdk_active;        // 1 пока плагин жив

    // ----- грузовик: float -----
    float speed;                // м/с (знак: <0 = задний ход)
    float engine_rpm;
    float engine_rpm_max;       // из конфигурации (rpm_limit)
    float user_steering;
    float user_throttle;
    float user_brake;
    float user_clutch;
    float eff_steering;
    float eff_throttle;
    float eff_brake;
    float eff_clutch;
    float cruise_control;       // м/с
    float fuel;                 // л
    float fuel_capacity;        // л
    float fuel_avg_consumption; // л/км
    float fuel_range;           // км
    float adblue;               // л
    float adblue_capacity;      // л
    float oil_pressure;         // psi
    float oil_temperature;      // °C
    float water_temperature;    // °C
    float battery_voltage;      // В
    float brake_air_pressure;   // psi
    float brake_temperature;    // °C
    float dashboard_backlight;  // 0..1
    float odometer;             // км
    float wear_engine;          // 0..1
    float wear_transmission;
    float wear_cabin;
    float wear_chassis;
    float wear_wheels;
    float nav_distance;         // м
    float nav_time;             // с
    float nav_speed_limit;      // м/с

    // векторы (локальные)
    float lin_vel[3];
    float ang_vel[3];
    float lin_acc[3];
    float ang_acc[3];

    // мировое положение
    double world_x;
    double world_y;
    double world_z;
    float world_heading;        // 0..1
    float world_pitch;
    float world_roll;

    // ----- прицеп -----
    float trailer_wear_chassis;
    float trailer_wear_wheels;
    float trailer_cargo_damage;

    // ----- работа -----
    float job_planned_distance_km;
    float cargo_mass;           // кг
    uint64_t job_income;
    uint32_t job_delivery_time; // игровые минуты (абс.)

    // ----- int -----
    int32_t  engine_gear;       // <0 = задняя
    int32_t  displayed_gear;
    uint32_t forward_gear_count;
    uint32_t reverse_gear_count;
    uint32_t hshifter_slot;
    uint32_t retarder_level;
    uint32_t retarder_step_count;
    uint32_t light_aux_front;
    uint32_t light_aux_roof;

    // ----- флаги (uint8) -----
    uint8_t electric_enabled;
    uint8_t engine_enabled;
    uint8_t parking_brake;
    uint8_t motor_brake;
    uint8_t differential_lock;
    uint8_t lift_axle;
    uint8_t lift_axle_indicator;
    uint8_t trailer_lift_axle;
    uint8_t fuel_warning;
    uint8_t adblue_warning;
    uint8_t oil_pressure_warning;
    uint8_t water_temperature_warning;
    uint8_t battery_voltage_warning;
    uint8_t air_pressure_warning;
    uint8_t air_pressure_emergency;
    uint8_t blinker_left_active;   // мигает сейчас
    uint8_t blinker_right_active;
    uint8_t blinker_left_on;       // рычаг включён
    uint8_t blinker_right_on;
    uint8_t light_parking;
    uint8_t light_low_beam;
    uint8_t light_high_beam;
    uint8_t light_beacon;
    uint8_t light_brake;
    uint8_t light_reverse;
    uint8_t wipers;
    uint8_t cruise_on;
    uint8_t trailer_connected;
    uint8_t job_cargo_loaded;
    uint8_t special_job;
    uint8_t _pad_flags[2];         // выравнивание (30 флагов + 2 = 32)

    // ----- строки (UTF-8, фикс. длина) -----
    char truck_brand[64];
    char truck_name[64];
    char truck_license[16];
    char trailer_name[64];
    char trailer_body_type[32];
    char cargo_id[64];
    char cargo_name[64];
    char source_city[64];
    char destination_city[64];
    char source_company[64];
    char destination_company[64];
    char game_version[16];
};
#pragma pack(pop)