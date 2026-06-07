using System.Collections.ObjectModel;

namespace Simryx.App.Models;

public sealed class GameGroup
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;

    // Состояние раскрытия группы (используется анимацией аккордеона).
    public bool IsExpanded { get; set; } = true;

    public ObservableCollection<RacingProfile> Profiles { get; } = new();

    public string CountText => Profiles.Count.ToString();
}