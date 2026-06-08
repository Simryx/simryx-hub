using System;
using System.IO;

namespace Simryx.Setup;

/// <summary>Константы и тексты установщика Simryx Hub.</summary>
public static class AppInfo
{
    public const string ProductName = "Simryx Hub";
    public const string Publisher = "Simryx";

    public const string Owner = "Simryx";
    public const string Repo = "simryx-hub";
    public const string AboutUrl = "https://github.com/Simryx/simryx-hub";

    // Должно совпадать со StartupService в самом приложении.
    public const string RunValueName = "SimryxHub";

    public const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SimryxHub";
    public const string RunKey =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Simryx Hub");

    public const string Description =
        "Один хаб для всего сим-рига — устройства, прошивки, телеметрия и профили Simryx.";

    public const string License =
@"ЛИЦЕНЗИОННОЕ СОГЛАШЕНИЕ С КОНЕЧНЫМ ПОЛЬЗОВАТЕЛЕМ (EULA)
Simryx Hub

1. Общие положения
Настоящее соглашение регулирует использование программного обеспечения
«Simryx Hub» (далее — ПО), правообладателем которого является Simryx.

2. Лицензия
Вам предоставляется неисключительное право использовать ПО на ваших
устройствах для настройки и обслуживания оборудования Simryx.

3. Ограничения
Запрещается декомпилировать, модифицировать и распространять ПО без
письменного разрешения правообладателя, за исключением случаев, прямо
разрешённых применимым законодательством.

4. Обновления
ПО может автоматически проверять и устанавливать обновления через
официальный репозиторий релизов Simryx.

5. Отказ от гарантий
ПО предоставляется «как есть». Правообладатель не несёт ответственности
за возможный ущерб, связанный с использованием ПО.

6. Контакты
GitHub: https://github.com/Simryx · E-mail: simryx_official@mail.ru

Устанавливая ПО, вы подтверждаете согласие с условиями данного соглашения.";
}