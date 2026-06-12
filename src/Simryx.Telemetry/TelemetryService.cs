using System;
using System.Threading;

namespace Simryx.Telemetry;

/// <summary>
/// Фоновое чтение телеметрии одной игры через её провайдера.
/// Опрашивает источник с фиксированной частотой (по умолчанию 60 Гц),
/// сам подключается/переподключается и поднимает события снимка и подключения.
/// Только чтение официальных интерфейсов игры — безопасно для анти-читов.
/// </summary>
public sealed class TelemetryService : IDisposable
{
    private readonly ITelemetryProvider _provider;
    private readonly int _periodMs;
    private readonly object _gate = new();

    private Timer? _timer;
    private int _busy;          // защита от наложения тиков
    private bool _connected;    // последнее известное состояние подключения
    private bool _disposed;

    /// <summary>Частота опроса, Гц.</summary>
    public int Hz { get; }

    /// <summary>Идентификатор игры провайдера.</summary>
    public string GameId => _provider.GameId;

    /// <summary>Подключён ли провайдер прямо сейчас.</summary>
    public bool IsConnected
    {
        get { lock (_gate) return _connected; }
    }

    /// <summary>Новый снимок телеметрии (вызывается из фонового потока таймера).</summary>
    public event Action<TelemetrySnapshot>? SnapshotReceived;

    /// <summary>Изменение факта подключения к источнику телеметрии.</summary>
    public event Action<bool>? ConnectionChanged;

    public TelemetryService(ITelemetryProvider provider, int hz = 60)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Hz = hz <= 0 ? 60 : hz;
        _periodMs = Math.Max(1, 1000 / Hz);
    }

    /// <summary>Запускает периодический опрос. Повторный вызов безопасен.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _timer ??= new Timer(Tick, null, 0, _periodMs);
        }
    }

    /// <summary>Останавливает опрос (провайдер не уничтожается).</summary>
    public void Stop()
    {
        Timer? t;
        lock (_gate)
        {
            t = _timer;
            _timer = null;
        }
        t?.Dispose();
    }

    private void Tick(object? _)
    {
        // Не накладываем тики, если предыдущий ещё считает.
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            // 1) Поддерживаем подключение (безопасно вызывать повторно).
            if (!_provider.IsConnected)
                _provider.TryConnect();

            // 2) Сообщаем об изменении факта подключения.
            UpdateConnection(_provider.IsConnected);

            // 3) Читаем снимок и поднимаем событие.
            if (_provider.IsConnected && _provider.TryRead(out var snapshot))
                SnapshotReceived?.Invoke(snapshot);
        }
        catch
        {
            // Чтение телеметрии не должно ронять приложение.
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void UpdateConnection(bool connected)
    {
        bool changed;
        lock (_gate)
        {
            changed = _connected != connected;
            _connected = connected;
        }
        if (changed) ConnectionChanged?.Invoke(connected);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Stop();
        try { _provider.Dispose(); } catch { }
    }
}