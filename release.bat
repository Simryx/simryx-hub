@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
title Simryx Hub - публикация релиза

REM ====== Настройки ======
set "REPO_DIR=C:\Users\Admin\simryx-hub"
set "OWNER=Simryx"
set "REPO=simryx-hub"
set "PROPS=Directory.Build.props"
set "API=https://api.github.com/repos/%OWNER%/%REPO%"

cd /d "%REPO_DIR%" 2>nul
if errorlevel 1 ( echo [ОШИБКА] Не найден репозиторий: %REPO_DIR% & pause & exit /b 1 )
where git >nul 2>nul || ( echo [ОШИБКА] git не найден в PATH. & pause & exit /b 1 )

:menu
cls
echo ==================================================
echo            Simryx Hub  -  публикация релиза
echo ==================================================
echo.
echo   Куда публикуем обновление?
echo.
echo     [1] Стабильная ветка      (тег vX.Y.Z)
echo     [2] Бета-ветка            (тег vX.Y.Z-beta.N)
echo     [3] Обновить версию       (пересобрать файлы существующего релиза)
echo     [0] Отмена
echo.
choice /c 1230 /n /m "   Ваш выбор: "
set "SEL=%errorlevel%"
if "%SEL%"=="4" goto cancel
if "%SEL%"=="1" ( set "MODE=stable" & goto getlatest )
if "%SEL%"=="2" ( set "MODE=beta"   & goto getlatest )
if "%SEL%"=="3" ( set "MODE=update" & goto modeupdate )
goto menu

:getlatest
echo.
echo   Запрашиваю последнюю версию на GitHub...
set "LATEST="
if "%MODE%"=="stable" (
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$h=@{'User-Agent'='SimryxHub';'Accept'='application/vnd.github+json'}; try{(Invoke-RestMethod -Headers $h -Uri '%API%/releases/latest').tag_name}catch{''}"`) do set "LATEST=%%i"
) else (
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$h=@{'User-Agent'='SimryxHub';'Accept'='application/vnd.github+json'}; try{$a=Invoke-RestMethod -Headers $h -Uri '%API%/releases?per_page=50'; ($a ^| Where-Object {$_.prerelease -and -not $_.draft} ^| Select-Object -First 1).tag_name}catch{''}"`) do set "LATEST=%%i"
)
if not defined LATEST set "LATEST=(релизов ещё нет)"
echo.
echo   Последняя версия в ветке "%MODE%":  !LATEST!
echo.
set "VER="
set /p "VER=   Введите НОВУЮ версию (например 0.3.0 или 0.3.0-beta.2): "
if not defined VER goto menu

for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$v='%VER%'.Trim(); if($v -notmatch '^[vV]'){$v='v'+$v}; $v"`) do set "TAG=%%i"
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "('%VER%' -replace '^[vV]','' -replace '[-+].*$','').Trim()"`) do set "NUMERIC=%%i"
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "('%VER%' -replace '^[vV]','').Trim()"`) do set "SEMVER=%%i"

echo.
echo   Будет создан / обновлён релиз:
echo       Тег:            %TAG%
echo       Версия сборки:  %SEMVER%
echo       Ветка:          %MODE%
echo.
choice /c YN /n /m "   Продолжить? (Y/N): "
if errorlevel 2 goto menu

echo.
echo   [1/5] Обновляю %PROPS%  ->  ^<Version^>%SEMVER%^</Version^>
powershell -NoProfile -Command "$p='%PROPS%'; $c=Get-Content $p -Raw; $c=[regex]::Replace($c,'<Version>.*?</Version>','<Version>%SEMVER%</Version>'); Set-Content $p $c -Encoding UTF8 -NoNewline"

echo   [2/5] Коммичу изменения...
git add -A
git commit -m "release: %TAG%" 1>nul 2>nul
if errorlevel 1 echo         (нет изменений для коммита - продолжаю)

echo   [3/5] Отправляю ветку main...
git push origin main || ( echo [ОШИБКА] git push main не удался. & pause & exit /b 1 )

echo   [4/5] Готовлю тег %TAG%...
git ls-remote --exit-code --tags origin "refs/tags/%TAG%" >nul 2>nul
if not errorlevel 1 (
echo         Тег уже существует - пересоздаю на текущем коммите...
git push origin :refs/tags/%TAG% >nul 2>nul
git tag -d %TAG% >nul 2>nul
)
git tag %TAG%

echo   [5/5] Отправляю тег ^(запустит сборку релиза^)...
git push origin %TAG% || ( echo [ОШИБКА] git push tag не удался. & pause & exit /b 1 )
goto done

:modeupdate
cls
echo   Существующие релизы на GitHub:
echo.
powershell -NoProfile -Command "$h=@{'User-Agent'='SimryxHub';'Accept'='application/vnd.github+json'}; try{$a=Invoke-RestMethod -Headers $h -Uri '%API%/releases?per_page=30'; $a | ForEach-Object{ '     {0,-24} {1}' -f $_.tag_name, $(if($_.prerelease){'(beta)'}else{'(stable)'}) }}catch{'     (не удалось получить список)'}"
echo.
set "VER="
set /p "VER=   Введите СУЩЕСТВУЮЩИЙ тег для пересборки (например v0.3.0): "
if not defined VER goto menu
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$v='%VER%'.Trim(); if($v -notmatch '^[vV]'){$v='v'+$v}; $v"`) do set "TAG=%%i"

echo.
echo   Пересборка релиза %TAG% из текущего кода (версия в теге не меняется).
choice /c YN /n /m "   Продолжить? (Y/N): "
if errorlevel 2 goto menu

git ls-remote --exit-code --tags origin "refs/tags/%TAG%" >nul 2>nul
if errorlevel 1 (
echo.
echo   [ОШИБКА] Тег %TAG% не найден. Для НОВОЙ версии используйте пункт 1 или 2.
pause & goto menu
)

echo   [1/4] Коммичу изменения...
git add -A
git commit -m "rebuild: %TAG%" 1>nul 2>nul
if errorlevel 1 echo         (нет изменений для коммита - продолжаю)
echo   [2/4] Отправляю ветку main...
git push origin main || ( echo [ОШИБКА] git push main. & pause & exit /b 1 )
echo   [3/4] Перемещаю тег %TAG% на текущий коммит...
git push origin :refs/tags/%TAG% >nul 2>nul
git tag -d %TAG% >nul 2>nul
git tag %TAG%
echo   [4/4] Отправляю тег (перезапустит сборку)...
git push origin %TAG% || ( echo [ОШИБКА] git push tag. & pause & exit /b 1 )
goto done

:done
echo.
echo ==================================================
echo   Готово! Сборка запущена на GitHub Actions:
echo     https://github.com/%OWNER%/%REPO%/actions
echo.
echo   После завершения в релизе %TAG% будут 2 файла:
echo     - Simryx.Hub.*-win-x64.zip   файл обновления (его читает приложение)
echo     - SimryxSetup.exe            установщик
echo ==================================================
echo.
pause
exit /b 0

:cancel
echo Отменено.
exit /b 0