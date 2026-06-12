cmake_minimum_required(VERSION 3.20)
project(SimryxScsPlugin LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(simryx_telemetry SHARED
    simryx_plugin.cpp
    simryx.def
)

# Заголовки SCS SDK скопировать в ./sdk/include (там лежат scssdk_telemetry.h, common/, eurotrucks2/, amtrucks/)
target_include_directories(simryx_telemetry PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/sdk/include")

set_target_properties(simryx_telemetry PROPERTIES OUTPUT_NAME "simryx_telemetry" PREFIX "")

if (MSVC)
    target_compile_definitions(simryx_telemetry PRIVATE _CRT_SECURE_NO_WARNINGS WIN32_LEAN_AND_MEAN)
endif()