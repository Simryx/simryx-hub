// Simryx SCS Telemetry plugin для ETS2 / ATS.
// Пишет выбранные каналы в memory-mapped файл SIMRYX_TRUCK_MMF_NAME.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstring>
#include <cstdint>
#include <cstdio>

#include "scssdk_telemetry.h"
#include "eurotrucks2/scssdk_eut2.h"
#include "eurotrucks2/scssdk_telemetry_eut2.h"
#include "amtrucks/scssdk_ats.h"
#include "amtrucks/scssdk_telemetry_ats.h"
#include "common/scssdk_telemetry_common_channels.h"
#include "common/scssdk_telemetry_common_configs.h"
#include "common/scssdk_telemetry_truck_common_channels.h"
#include "common/scssdk_telemetry_trailer_common_channels.h"
#include "common/scssdk_telemetry_job_common_channels.h"
#include "eurotrucks2/scssdk_eut2.h"
#include "eurotrucks2/scssdk_telemetry_eut2.h"
#include "amtrucks/scssdk_ats.h"
#include "amtrucks/scssdk_telemetry_ats.h"

#include "simryx_shared.h"

// ===== глобальное состояние =====
static HANDLE                                  g_map  = nullptr;
static simryx_truck_shared_t*                  g_data = nullptr;
static scs_telemetry_register_for_channel_t    g_reg  = nullptr;
static scs_log_t                               g_log  = nullptr;

// ===== обобщённые колбэки записи по типам =====
static SCSAPI_VOID store_float(const scs_string_t, const scs_u32_t,
                               const scs_value_t* const value, const scs_context_t ctx)
{
    if (!ctx) return;
    *static_cast<float*>(ctx) =
        (value && value->type == SCS_VALUE_TYPE_float) ? value->value_float.value : 0.0f;
}

static SCSAPI_VOID store_s32(const scs_string_t, const scs_u32_t,
                             const scs_value_t* const value, const scs_context_t ctx)
{
    if (!ctx) return;
    *static_cast<int32_t*>(ctx) =
        (value && value->type == SCS_VALUE_TYPE_s32) ? value->value_s32.value : 0;
}

static SCSAPI_VOID store_u32(const scs_string_t, const scs_u32_t,
                             const scs_value_t* const value, const scs_context_t ctx)
{
    if (!ctx) return;
    *static_cast<uint32_t*>(ctx) =
        (value && value->type == SCS_VALUE_TYPE_u32) ? value->value_u32.value : 0u;
}

static SCSAPI_VOID store_bool(const scs_string_t, const scs_u32_t,
                              const scs_value_t* const value, const scs_context_t ctx)
{
    if (!ctx) return;
    *static_cast<uint8_t*>(ctx) =
        (value && value->type == SCS_VALUE_TYPE_bool && value->value_bool.value) ? 1 : 0;
}

static SCSAPI_VOID store_fvector(const scs_string_t, const scs_u32_t,
                                 const scs_value_t* const value, const scs_context_t ctx)
{
    if (!ctx) return;
    float* dst = static_cast<float*>(ctx);
    if (value && value->type == SCS_VALUE_TYPE_fvector)
    {
        dst[0] = value->value_fvector.x;
        dst[1] = value->value_fvector.y;
        dst[2] = value->value_fvector.z;
    }
    else { dst[0] = dst[1] = dst[2] = 0.0f; }
}

static SCSAPI_VOID store_cruise(const scs_string_t, const scs_u32_t,
                                const scs_value_t* const value, const scs_context_t)
{
    if (!g_data) return;
    float v = (value && value->type == SCS_VALUE_TYPE_float) ? value->value_float.value : 0.0f;
    g_data->cruise_control = v;
    g_data->cruise_on = (v > 0.1f) ? 1 : 0;
}

static SCSAPI_VOID store_world(const scs_string_t, const scs_u32_t,
                               const scs_value_t* const value, const scs_context_t)
{
    if (!g_data) return;
    if (value && value->type == SCS_VALUE_TYPE_dplacement)
    {
        g_data->world_x = value->value_dplacement.position.x;
        g_data->world_y = value->value_dplacement.position.y;
        g_data->world_z = value->value_dplacement.position.z;
        g_data->world_heading = value->value_dplacement.orientation.heading;
        g_data->world_pitch   = value->value_dplacement.orientation.pitch;
        g_data->world_roll    = value->value_dplacement.orientation.roll;
    }
}

// ===== обработка конфигурации (строки + параметры) =====
static void copy_str(char* dst, size_t cap, const scs_value_t& v)
{
    if (v.type == SCS_VALUE_TYPE_string && v.value_string.value)
    {
        strncpy(dst, v.value_string.value, cap - 1);
        dst[cap - 1] = 0;
    }
}

static void handle_truck_cfg(const scs_telemetry_configuration_t* cfg)
{
    for (const scs_named_value_t* a = cfg->attributes; a->name; ++a)
    {
        if      (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_brand))               copy_str(g_data->truck_brand, sizeof(g_data->truck_brand), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_name))                copy_str(g_data->truck_name, sizeof(g_data->truck_name), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_license_plate))       copy_str(g_data->truck_license, sizeof(g_data->truck_license), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_fuel_capacity))       g_data->fuel_capacity   = a->value.value_float.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_adblue_capacity))     g_data->adblue_capacity = a->value.value_float.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_rpm_limit))           g_data->engine_rpm_max  = a->value.value_float.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_forward_gear_count))  g_data->forward_gear_count = a->value.value_u32.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_reverse_gear_count))  g_data->reverse_gear_count = a->value.value_u32.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_retarder_step_count)) g_data->retarder_step_count = a->value.value_u32.value;
    }
}

static void handle_trailer_cfg(const scs_telemetry_configuration_t* cfg)
{
    for (const scs_named_value_t* a = cfg->attributes; a->name; ++a)
    {
        if      (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_name))      copy_str(g_data->trailer_name, sizeof(g_data->trailer_name), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_body_type)) copy_str(g_data->trailer_body_type, sizeof(g_data->trailer_body_type), a->value);
    }
}

static void handle_job_cfg(const scs_telemetry_configuration_t* cfg)
{
    for (const scs_named_value_t* a = cfg->attributes; a->name; ++a)
    {
        if      (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_cargo_id))            copy_str(g_data->cargo_id, sizeof(g_data->cargo_id), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_cargo))               copy_str(g_data->cargo_name, sizeof(g_data->cargo_name), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_cargo_mass))          g_data->cargo_mass = a->value.value_float.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_source_city))         copy_str(g_data->source_city, sizeof(g_data->source_city), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_destination_city))    copy_str(g_data->destination_city, sizeof(g_data->destination_city), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_source_company))      copy_str(g_data->source_company, sizeof(g_data->source_company), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_destination_company)) copy_str(g_data->destination_company, sizeof(g_data->destination_company), a->value);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_income))              g_data->job_income = a->value.value_u64.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_delivery_time))       g_data->job_delivery_time = a->value.value_u32.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_planned_distance_km)) g_data->job_planned_distance_km = (float)a->value.value_u32.value;
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_is_cargo_loaded))     g_data->job_cargo_loaded = (a->value.value_bool.value ? 1 : 0);
        else if (!strcmp(a->name, SCS_TELEMETRY_CONFIG_ATTRIBUTE_special_job))         g_data->special_job = (a->value.value_bool.value ? 1 : 0);
    }
}

// ===== события =====
static SCSAPI_VOID event_cb(const scs_event_t event, const void* const info, const scs_context_t)
{
    if (!g_data) return;

    if (event == SCS_TELEMETRY_EVENT_paused)  { g_data->paused = 1; return; }
    if (event == SCS_TELEMETRY_EVENT_started) { g_data->paused = 0; return; }

    if (event == SCS_TELEMETRY_EVENT_configuration)
    {
        const scs_telemetry_configuration_t* cfg =
            static_cast<const scs_telemetry_configuration_t*>(info);
        if (!cfg || !cfg->id) return;

        if (!strcmp(cfg->id, SCS_TELEMETRY_CONFIG_truck))      handle_truck_cfg(cfg);
        else if (!strcmp(cfg->id, SCS_TELEMETRY_CONFIG_job))   handle_job_cfg(cfg);
        else if (!strcmp(cfg->id, SCS_TELEMETRY_CONFIG_trailer) ||
                 !strcmp(cfg->id, "trailer.0"))                handle_trailer_cfg(cfg);
    }
}

// ===== вспомогательная регистрация (ошибки игнорируем) =====
static inline void reg(const scs_string_t name, scs_value_type_t type,
                       scs_telemetry_channel_callback_t cb, void* ctx)
{
    if (g_reg) g_reg(name, SCS_U32_NIL, type, SCS_TELEMETRY_CHANNEL_FLAG_none, cb, ctx);
}

// ===== точка входа =====
extern "C" SCSAPI_RESULT scs_telemetry_init(const scs_u32_t version, const scs_telemetry_init_params_t *const params)
{
    if (version < SCS_TELEMETRY_VERSION_1_00) return SCS_RESULT_unsupported;

    const scs_telemetry_init_params_v100_t* const p =
        static_cast<const scs_telemetry_init_params_v100_t*>(params);

    g_log = p->common.log;
    g_reg = p->register_for_channel;

    // --- создаём shared memory ---
    g_map = CreateFileMappingA(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE,
                               0, sizeof(simryx_truck_shared_t), SIMRYX_TRUCK_MMF_NAME);
    if (!g_map) return SCS_RESULT_generic_error;

    g_data = static_cast<simryx_truck_shared_t*>(
        MapViewOfFile(g_map, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(simryx_truck_shared_t)));
    if (!g_data) { CloseHandle(g_map); g_map = nullptr; return SCS_RESULT_generic_error; }

    memset(g_data, 0, sizeof(simryx_truck_shared_t));
    g_data->structure_version = SIMRYX_TRUCK_STRUCT_VERSION;
    g_data->sdk_active = 1;

    if (p->common.game_id && !strcmp(p->common.game_id, SCS_GAME_ID_EUT2))      g_data->game_id = 1;
    else if (p->common.game_id && !strcmp(p->common.game_id, SCS_GAME_ID_ATS))  g_data->game_id = 2;
    else                                                                        g_data->game_id = 0;

    snprintf(g_data->game_version, sizeof(g_data->game_version), "%u.%u",
             (unsigned)SCS_GET_MAJOR_VERSION(p->common.game_version),
             (unsigned)SCS_GET_MINOR_VERSION(p->common.game_version));

    // --- события ---
    p->register_for_event(SCS_TELEMETRY_EVENT_configuration, event_cb, nullptr);
    p->register_for_event(SCS_TELEMETRY_EVENT_paused,        event_cb, nullptr);
    p->register_for_event(SCS_TELEMETRY_EVENT_started,       event_cb, nullptr);

    // --- общие каналы ---
    reg(SCS_TELEMETRY_CHANNEL_game_time, SCS_VALUE_TYPE_u32, store_u32, &g_data->time_abs_minutes);

    // --- грузовик: ввод/движение ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_speed,             SCS_VALUE_TYPE_float, store_float, &g_data->speed);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_engine_rpm,        SCS_VALUE_TYPE_float, store_float, &g_data->engine_rpm);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_engine_gear,       SCS_VALUE_TYPE_s32,   store_s32,   &g_data->engine_gear);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_displayed_gear,    SCS_VALUE_TYPE_s32,   store_s32,   &g_data->displayed_gear);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_input_steering,    SCS_VALUE_TYPE_float, store_float, &g_data->user_steering);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_input_throttle,    SCS_VALUE_TYPE_float, store_float, &g_data->user_throttle);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_input_brake,       SCS_VALUE_TYPE_float, store_float, &g_data->user_brake);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_input_clutch,      SCS_VALUE_TYPE_float, store_float, &g_data->user_clutch);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_effective_steering,SCS_VALUE_TYPE_float, store_float, &g_data->eff_steering);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_effective_throttle,SCS_VALUE_TYPE_float, store_float, &g_data->eff_throttle);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_effective_brake,   SCS_VALUE_TYPE_float, store_float, &g_data->eff_brake);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_effective_clutch,  SCS_VALUE_TYPE_float, store_float, &g_data->eff_clutch);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_cruise_control,    SCS_VALUE_TYPE_float, store_cruise,nullptr);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_hshifter_slot,     SCS_VALUE_TYPE_u32,   store_u32,   &g_data->hshifter_slot);

    // --- грузовик: тормоза/двигатель/трансмиссия ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_parking_brake,     SCS_VALUE_TYPE_bool,  store_bool,  &g_data->parking_brake);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_motor_brake,       SCS_VALUE_TYPE_bool,  store_bool,  &g_data->motor_brake);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_retarder_level,    SCS_VALUE_TYPE_u32,   store_u32,   &g_data->retarder_level);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_differential_lock, SCS_VALUE_TYPE_bool,  store_bool,  &g_data->differential_lock);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_lift_axle,         SCS_VALUE_TYPE_bool,  store_bool,  &g_data->lift_axle);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_lift_axle_indicator,SCS_VALUE_TYPE_bool, store_bool,  &g_data->lift_axle_indicator);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_trailer_lift_axle, SCS_VALUE_TYPE_bool,  store_bool,  &g_data->trailer_lift_axle);

    // --- грузовик: расходники/датчики ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_fuel,                       SCS_VALUE_TYPE_float, store_float, &g_data->fuel);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_fuel_warning,               SCS_VALUE_TYPE_bool,  store_bool,  &g_data->fuel_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_fuel_average_consumption,   SCS_VALUE_TYPE_float, store_float, &g_data->fuel_avg_consumption);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_fuel_range,                 SCS_VALUE_TYPE_float, store_float, &g_data->fuel_range);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_adblue,                     SCS_VALUE_TYPE_float, store_float, &g_data->adblue);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_adblue_warning,             SCS_VALUE_TYPE_bool,  store_bool,  &g_data->adblue_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_oil_pressure,               SCS_VALUE_TYPE_float, store_float, &g_data->oil_pressure);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_oil_pressure_warning,       SCS_VALUE_TYPE_bool,  store_bool,  &g_data->oil_pressure_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_oil_temperature,            SCS_VALUE_TYPE_float, store_float, &g_data->oil_temperature);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_water_temperature,          SCS_VALUE_TYPE_float, store_float, &g_data->water_temperature);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_water_temperature_warning,  SCS_VALUE_TYPE_bool,  store_bool,  &g_data->water_temperature_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_battery_voltage,            SCS_VALUE_TYPE_float, store_float, &g_data->battery_voltage);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_battery_voltage_warning,    SCS_VALUE_TYPE_bool,  store_bool,  &g_data->battery_voltage_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_brake_air_pressure,         SCS_VALUE_TYPE_float, store_float, &g_data->brake_air_pressure);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_brake_air_pressure_warning, SCS_VALUE_TYPE_bool,  store_bool,  &g_data->air_pressure_warning);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_brake_air_pressure_emergency,SCS_VALUE_TYPE_bool, store_bool,  &g_data->air_pressure_emergency);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_brake_temperature,          SCS_VALUE_TYPE_float, store_float, &g_data->brake_temperature);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_dashboard_backlight,        SCS_VALUE_TYPE_float, store_float, &g_data->dashboard_backlight);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_electric_enabled,           SCS_VALUE_TYPE_bool,  store_bool,  &g_data->electric_enabled);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_engine_enabled,             SCS_VALUE_TYPE_bool,  store_bool,  &g_data->engine_enabled);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_odometer,                   SCS_VALUE_TYPE_float, store_float, &g_data->odometer);

    // --- грузовик: свет ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_lblinker,        SCS_VALUE_TYPE_bool, store_bool, &g_data->blinker_left_on);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_rblinker,        SCS_VALUE_TYPE_bool, store_bool, &g_data->blinker_right_on);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_lblinker,  SCS_VALUE_TYPE_bool, store_bool, &g_data->blinker_left_active);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_rblinker,  SCS_VALUE_TYPE_bool, store_bool, &g_data->blinker_right_active);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_parking,   SCS_VALUE_TYPE_bool, store_bool, &g_data->light_parking);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_low_beam,  SCS_VALUE_TYPE_bool, store_bool, &g_data->light_low_beam);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_high_beam, SCS_VALUE_TYPE_bool, store_bool, &g_data->light_high_beam);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_aux_front, SCS_VALUE_TYPE_u32,  store_u32,  &g_data->light_aux_front);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_aux_roof,  SCS_VALUE_TYPE_u32,  store_u32,  &g_data->light_aux_roof);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_beacon,    SCS_VALUE_TYPE_bool, store_bool, &g_data->light_beacon);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_brake,     SCS_VALUE_TYPE_bool, store_bool, &g_data->light_brake);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_light_reverse,   SCS_VALUE_TYPE_bool, store_bool, &g_data->light_reverse);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wipers,          SCS_VALUE_TYPE_bool, store_bool, &g_data->wipers);

    // --- грузовик: износ ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wear_engine,       SCS_VALUE_TYPE_float, store_float, &g_data->wear_engine);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wear_transmission, SCS_VALUE_TYPE_float, store_float, &g_data->wear_transmission);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wear_cabin,        SCS_VALUE_TYPE_float, store_float, &g_data->wear_cabin);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wear_chassis,      SCS_VALUE_TYPE_float, store_float, &g_data->wear_chassis);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_wear_wheels,       SCS_VALUE_TYPE_float, store_float, &g_data->wear_wheels);

    // --- грузовик: физика/позиция ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_local_linear_velocity,      SCS_VALUE_TYPE_fvector,    store_fvector, g_data->lin_vel);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_local_angular_velocity,     SCS_VALUE_TYPE_fvector,    store_fvector, g_data->ang_vel);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_local_linear_acceleration,  SCS_VALUE_TYPE_fvector,    store_fvector, g_data->lin_acc);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_local_angular_acceleration, SCS_VALUE_TYPE_fvector,    store_fvector, g_data->ang_acc);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_world_placement,            SCS_VALUE_TYPE_dplacement, store_world,   nullptr);

    // --- навигация ---
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_navigation_distance,    SCS_VALUE_TYPE_float, store_float, &g_data->nav_distance);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_navigation_time,        SCS_VALUE_TYPE_float, store_float, &g_data->nav_time);
    reg(SCS_TELEMETRY_TRUCK_CHANNEL_navigation_speed_limit, SCS_VALUE_TYPE_float, store_float, &g_data->nav_speed_limit);

    // --- прицеп (первый) ---
    reg(SCS_TELEMETRY_TRAILER_CHANNEL_connected,    SCS_VALUE_TYPE_bool,  store_bool,  &g_data->trailer_connected);
    reg(SCS_TELEMETRY_TRAILER_CHANNEL_wear_chassis, SCS_VALUE_TYPE_float, store_float, &g_data->trailer_wear_chassis);
    reg(SCS_TELEMETRY_TRAILER_CHANNEL_wear_wheels,  SCS_VALUE_TYPE_float, store_float, &g_data->trailer_wear_wheels);
    reg(SCS_TELEMETRY_TRAILER_CHANNEL_cargo_damage, SCS_VALUE_TYPE_float, store_float, &g_data->trailer_cargo_damage);

    if (g_log) g_log(SCS_LOG_TYPE_message, "[Simryx] telemetry plugin initialized");
    return SCS_RESULT_ok;
}

extern "C" SCSAPI_VOID scs_telemetry_shutdown(void)
{
    if (g_data) { g_data->sdk_active = 0; UnmapViewOfFile(g_data); g_data = nullptr; }
    if (g_map)  { CloseHandle(g_map); g_map = nullptr; }
    g_reg = nullptr;
    g_log = nullptr;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }