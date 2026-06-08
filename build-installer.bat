@echo off
chcp 65001 >nul
cd /d "C:\Users\Admin\simryx-hub"

echo === Сборка установщика Simryx Hub ===
dotnet publish installer\Simryx.Setup\Simryx.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o installer-out
if errorlevel 1 (
    echo ОШИБКА сборки.
    pause
    exit /b 1
)

echo.
echo Готово: installer-out\SimryxSetup.exe
echo Запусти его для теста установки, либо с ключом --uninstall для проверки удаления.
pause