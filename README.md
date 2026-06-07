# Simryx Hub

Единое ПО для управления симрейсинг-устройствами Simryx — калибровка, телеметрия,
профили под игры, обновление прошивок и автообновление приложения.

> One hub for your whole rig.

## Требования к сборке
- Windows 10 (2004+) / Windows 11
- .NET SDK 8.0.421 (см. `global.json`)
- Visual Studio 2022 + компонент «Windows App SDK C# Templates»

## Структура решения
- `src/Simryx.App` — UI на WinUI 3 (.NET 8)
- `src/Simryx.Core` — хост, оркестрация, DI
- `src/Simryx.Common` — модели, контракты, логирование
- `src/Simryx.Devices` — работа с HID/WinUSB
- `src/Simryx.Telemetry` — движок телеметрии + адаптеры игр
- `src/Simryx.Profiles` — профили устройств
- `src/Simryx.Firmware` — обновление и подпись прошивок
- `src/Simryx.Security` — серийники, защита, криптография
- `src/Simryx.Updater` — каналы обновления приложения
- `tests/*` — модульные тесты (xUnit)

## Быстрый старт