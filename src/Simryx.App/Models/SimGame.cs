namespace Simryx.App.Models;

public sealed class SimGame
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // Имена процессов игры — задел под будущее авто-определение запущенной игры.
    public string[] ProcessNames { get; init; } = System.Array.Empty<string>();

    public override string ToString() => Name;
}