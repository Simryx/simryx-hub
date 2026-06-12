using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simryx.App.Models;
using Simryx.App.Services;
using Simryx.App.Views.Telemetry;
using Simryx.Telemetry;

namespace Simryx.App.Views;

public sealed partial class TelemetryPage : Page
{
    private GameDetectionService? _detection;
    private UnitsService? _units;
    private TelemetrySnapshot? _last;
    private readonly bool _en;

    // Активный экран телеметрии и id игры, для которой он создан.
    private ITelemetryGameView? _gameView;
    private string _hostGameId = "";

    public TelemetryPage()
    {
        InitializeComponent();
        var lang = App.Services.GetService<ILocalSettingsService>()?.Read<string>("AppLanguage") ?? "ru-RU";
        _en = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _units = App.Services.GetService<UnitsService>();
        if (_units is not null) _units.Changed += OnUnitsChanged;

        _detection = App.Services.GetService<GameDetectionService>();
        if (_detection is null) { ShowEmpty(); return; }
        _detection.GameStarted += OnGameStarted;
        _detection.GameStopped += OnGameStopped;
        _detection.SnapshotReceived += OnSnapshot;
        _detection.TelemetryConnectionChanged += OnConnection;

        var game = _detection.CurrentGame;
        if (game is not null) ShowGame(game); else ShowEmpty();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_units is not null) { _units.Changed -= OnUnitsChanged; _units = null; }
        if (_detection is null) return;
        _detection.GameStarted -= OnGameStarted;
        _detection.GameStopped -= OnGameStopped;
        _detection.SnapshotReceived -= OnSnapshot;
        _detection.TelemetryConnectionChanged -= OnConnection;
        _detection = null;
    }

    private void OnGameStarted(SimGame game) => Enqueue(() => ShowGame(game));
    private void OnGameStopped(SimGame game) => Enqueue(ShowEmpty);
    private void OnConnection(bool connected) => Enqueue(() => { if (!connected) ShowWaiting(); });
    private void OnSnapshot(TelemetrySnapshot s) => Enqueue(() => Render(s));
    private void OnUnitsChanged() => Enqueue(() => { _gameView?.SetUnits(_units); if (_last is not null) Render(_last); });
    private void Enqueue(Action action) => DispatcherQueue.TryEnqueue(() => action());

    private void ShowEmpty()
    {
        EmptyState.Visibility = Visibility.Visible;
        WaitingText.Visibility = Visibility.Collapsed;
        GameHost.Visibility = Visibility.Collapsed;
    }

    private void ShowGame(SimGame game)
    {
        EnsureView(game.Id);
        ShowWaiting();
    }

    private void ShowWaiting()
    {
        EmptyState.Visibility = Visibility.Visible;
        GameHost.Visibility = Visibility.Collapsed;
        WaitingText.Visibility = Visibility.Visible;
        WaitingText.Text = _en
            ? "Game detected. Waiting for an active session…"
            : "Игра обнаружена. Ждём активную сессию…";
    }

    private void ShowUnsupported()
    {
        EmptyState.Visibility = Visibility.Visible;
        GameHost.Visibility = Visibility.Collapsed;
        WaitingText.Visibility = Visibility.Visible;
        WaitingText.Text = _en
            ? "Telemetry screen for this game is not ready yet."
            : "Экран телеметрии для этой игры ещё не готов.";
    }

    // Создаёт/переключает экран телеметрии под конкретную игру.
    private void EnsureView(string gameId)
    {
        if (_hostGameId == gameId && _gameView is not null) return;

        _gameView = CreateView(gameId);
        _hostGameId = gameId;
        GameHost.Content = _gameView as UIElement;
        _gameView?.SetUnits(_units);
    }

    private static ITelemetryGameView? CreateView(string gameId) => gameId switch
    {
        "assetto_corsa" => new AssettoCorsaTelemetryView(),
        "ets2" => new EuroTruck2TelemetryView("ets2"),
        "ats"  => new EuroTruck2TelemetryView("ats"),
        _ => null,
    };

    private void Render(TelemetrySnapshot s)
    {
        _last = s;
        if (s.Status != TelemetryStatus.Live) { ShowWaiting(); return; }

        EnsureView(s.GameId);
        if (_gameView is null) { ShowUnsupported(); return; }

        EmptyState.Visibility = Visibility.Collapsed;
        WaitingText.Visibility = Visibility.Collapsed;
        GameHost.Visibility = Visibility.Visible;

        _gameView.Render(s);
    }
}