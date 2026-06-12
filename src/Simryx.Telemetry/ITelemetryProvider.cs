using System;

namespace Simryx.Telemetry;

/// <summary>
/// Источник телеметрии конкретной игры. ТОЛЬКО чтение официальных интерфейсов игры
/// (shared memory / UDP) — без чтения чужой памяти, инъекций и хуков. Безопасно для анти-читов.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>Идентификатор игры (совпадает с SimGame.Id).</summary>
    string GameId { get; }

    /// <summary>Подключён ли провайдер к источнику.</summary>
    bool IsConnected { get; }

    /// <summary>Пытается подключиться (открыть shared memory). Безопасно вызывать повторно.</summary>
    bool TryConnect();

    /// <summary>Читает текущий снимок. false — если данных нет (игра не запущена / не в сессии).</summary>
    bool TryRead(out TelemetrySnapshot snapshot);
}