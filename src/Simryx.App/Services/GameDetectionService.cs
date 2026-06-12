using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Simryx.App.Models;
using Simryx.Telemetry;

namespace Simryx.App.Services;

/// <summary>
/// Фоновое определение запущенной сим-гонки по списку процессов ОС.
/// Безопасно: читаем только перечень процессов (как диспетчер задач), в игру не лезем.
/// При обнаружении игры применяет её приоритетный профиль и, если для игры есть
/// провайдер телеметрии, запускает чтение. UI-независим: подписчик сам маршалит в UI-поток.
/// </summary>
public sealed class GameDetectionService : IDisposable
{
    private readonly ProfileService _profiles = new();
    private readonly object _gate = new();
    private Timer? _timer;
    private int _busy;

    private SimGame? _current;
    private TelemetryService? _telemetry;

    /// <summary>Игра обнаружена (процесс запущен).</summary>
    public event Action<SimGame>? GameStarted;

    /// <summary>Игра закрыта.</summary>
    public event Action<SimGame>? GameStopped;

    /// <summary>Новый снимок телеметрии текущей игры.</summary>
    public event Action<TelemetrySnapshot>? SnapshotReceived;

    /// <summary>Изменение факта подключения к телеметрии текущей игры.</summary>
    public event Action<bool>? TelemetryConnectionChanged;

    /// <summary>Текущая обнаруженная игра (или null).</summary>
    public SimGame? CurrentGame
    {
        get { lock (_gate) return _current; }
    }

    public void Start(int pollMs = 1500) => _timer ??= new Timer(Tick, null, 0, pollMs);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Tick(object? _)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return; // не накладываем тики
        try
        {
            var running = DetectRunningGame();

            // Та же игра (или по-прежнему ничего) — выходим.
            if (running?.Id == _current?.Id) return;

            if (_current is not null) HandleStopped(_current);
            if (running is not null) HandleStarted(running);
        }
        catch
        {
            // Детекция не должна ронять приложение.
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static SimGame? DetectRunningGame()
    {
        // Снимок имён процессов (без .exe), в нижнем регистре.
        var names = Process.GetProcesses()
            .Select(p =>
            {
                try { return p.ProcessName; }
                catch { return null; }
                finally { try { p.Dispose(); } catch { } }
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!.ToLowerInvariant())
            .ToHashSet();

        foreach (var game in GameCatalog.All)
        {
            foreach (var proc in game.ProcessNames)
            {
                if (string.IsNullOrEmpty(proc)) continue;
                if (names.Contains(proc.ToLowerInvariant()))
                    return game;
            }
        }
        return null;
    }

    private void HandleStarted(SimGame game)
    {
        lock (_gate) _current = game;

        // 1) Авто-профиль: применяем приоритетный профиль игры (если назначен).
        try
        {
            var priorityId = _profiles.GetPriorityId(game.Id);
            if (!string.IsNullOrWhiteSpace(priorityId))
                _profiles.SetActive(priorityId);
        }
        catch { /* отсутствие профиля не критично */ }

        // 2a) Для SCS-игр (ETS2/ATS) докидываем наш плагин в папку plugins игры.
//     Игра подхватит его при следующем запуске.
if (game.Id is "ets2" or "ats")
	ScsPluginInstaller.EnsureInstalledFor(game.ProcessNames);
        var provider = CreateProvider(game.Id);
        if (provider is not null)
        {
            var svc = new TelemetryService(provider, hz: 60);
            svc.SnapshotReceived += OnSnapshot;
            svc.ConnectionChanged += OnTelemetryConnection;
            _telemetry = svc;
            svc.Start();
        }

        GameStarted?.Invoke(game);
    }

    private void HandleStopped(SimGame game)
    {
        if (_telemetry is not null)
        {
            _telemetry.SnapshotReceived -= OnSnapshot;
            _telemetry.ConnectionChanged -= OnTelemetryConnection;
            _telemetry.Dispose();
            _telemetry = null;
            TelemetryConnectionChanged?.Invoke(false);
        }

        lock (_gate) _current = null;
        GameStopped?.Invoke(game);
    }

    private void OnSnapshot(TelemetrySnapshot snap) => SnapshotReceived?.Invoke(snap);
    private void OnTelemetryConnection(bool connected) => TelemetryConnectionChanged?.Invoke(connected);

    // Фабрика провайдеров: пока поддержан только Assetto Corsa. Дальше добавим acc, iracing и т.д.
    private static ITelemetryProvider? CreateProvider(string gameId) => gameId switch
{
	AssettoCorsaTelemetryProvider.GameIdConst => new AssettoCorsaTelemetryProvider(),
	EuroTruck2TelemetryProvider.Ets2IdConst => new EuroTruck2TelemetryProvider(EuroTruck2TelemetryProvider.Ets2IdConst),
	EuroTruck2TelemetryProvider.AtsIdConst  => new EuroTruck2TelemetryProvider(EuroTruck2TelemetryProvider.AtsIdConst),
	_ => null,
};

    public void Dispose()
    {
        Stop();
        if (_telemetry is not null)
        {
            _telemetry.Dispose();
            _telemetry = null;
        }
    }
}