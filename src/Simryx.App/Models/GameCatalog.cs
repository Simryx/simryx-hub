using System;
using System.Collections.Generic;
using System.Linq;

namespace Simryx.App.Models;

public static class GameCatalog
{
    public static IReadOnlyList<SimGame> All { get; } = new List<SimGame>
    {
        new() { Id = "assetto_corsa",      Name = "Assetto Corsa",                 ProcessNames = new[] { "acs" } },
        new() { Id = "acc",                Name = "Assetto Corsa Competizione",    ProcessNames = new[] { "AC2-Win64-Shipping" } },
        new() { Id = "ams2",               Name = "Automobilista 2",               ProcessNames = new[] { "AMS2AVX", "AMS2" } },
        new() { Id = "rfactor2",           Name = "rFactor 2",                     ProcessNames = new[] { "rFactor2" } },
        new() { Id = "lmu",                Name = "Le Mans Ultimate",              ProcessNames = new[] { "Le Mans Ultimate" } },
        new() { Id = "iracing",            Name = "iRacing",                       ProcessNames = new[] { "iRacingSim64DX11", "iRacingSim64" } },
        new() { Id = "raceroom",           Name = "RaceRoom Racing Experience",    ProcessNames = new[] { "RRRE64", "RRRE" } },
        new() { Id = "f1_24",              Name = "F1 24",                         ProcessNames = new[] { "F1_24", "F1_24_dx12" } },
        new() { Id = "f1_23",              Name = "F1 23",                         ProcessNames = new[] { "F1_23", "F1_23_dx12" } },
        new() { Id = "dirt_rally_2",       Name = "DiRT Rally 2.0",                ProcessNames = new[] { "dirtrally2" } },
        new() { Id = "ea_wrc",             Name = "EA Sports WRC",                 ProcessNames = new[] { "wrc" } },
        new() { Id = "beamng",             Name = "BeamNG.drive",                  ProcessNames = new[] { "BeamNG.drive" } },
        new() { Id = "forza_motorsport",   Name = "Forza Motorsport",              ProcessNames = new[] { "ForzaMotorsport" } },
        new() { Id = "forza_horizon_5",    Name = "Forza Horizon 5",               ProcessNames = new[] { "ForzaHorizon5" } },
        new() { Id = "ets2",               Name = "Euro Truck Simulator 2",        ProcessNames = new[] { "eurotrucks2" } },
        new() { Id = "ats",                Name = "American Truck Simulator",      ProcessNames = new[] { "amtrucks" } },
        new() { Id = "wreckfest",          Name = "Wreckfest",                     ProcessNames = new[] { "Wreckfest" } },
        new() { Id = "other",              Name = "Другая игра",                   ProcessNames = Array.Empty<string>() },
    };

    public static SimGame? FindById(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(g => g.Id == id);

    public static SimGame? FindByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : All.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    // Определяет Id игры для профиля; для старых профилей (без Id) восстанавливает по названию.
    public static string ResolveGameId(RacingProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.GameId)) return profile.GameId;
        var byName = FindByName(profile.Game);
        return byName?.Id ?? "other";
    }
}