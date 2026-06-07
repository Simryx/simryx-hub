using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace Simryx.App.Models;

public sealed class RacingProfile : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    // Стабильный идентификатор игры из каталога (используется для группировки и авто-смены профиля).
    public string GameId { get; set; } = string.Empty;

    // Отображаемое название игры.
    public string Game { get; set; } = string.Empty;

    public int Sensitivity { get; set; } = 50;
    public int ForceFeedback { get; set; } = 70;
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string GameDisplay => string.IsNullOrWhiteSpace(Game) ? "—" : Game;

    [JsonIgnore]
    public string StatsDisplay => $"{Sensitivity}% · {ForceFeedback}%";

    private bool _isPriority;

    [JsonIgnore]
    public bool IsPriority
    {
        get => _isPriority;
        set
        {
            if (_isPriority == value) return;
            _isPriority = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PriorityChipVisibility));
            OnPropertyChanged(nameof(SetPriorityButtonVisibility));
        }
    }

    [JsonIgnore]
    public Visibility PriorityChipVisibility => _isPriority ? Visibility.Visible : Visibility.Collapsed;

    [JsonIgnore]
    public Visibility SetPriorityButtonVisibility => _isPriority ? Visibility.Collapsed : Visibility.Visible;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}