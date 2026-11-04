// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Map;

namespace Rts.Game.Saves;

/// <summary>A save read back into a running simulation, with the AI re-attached and (for a mission) the script host restored. Made by <see cref="GameLoader"/>.</summary>
public sealed class LoadedGame
{
    internal LoadedGame(GameSaveHeader header, Simulation sim, MapData map, AiDriver? ai, MissionHost? host, string rulesDir, string note)
    {
        Header = header;
        Sim = sim;
        Map = map;
        Ai = ai;
        Host = host;
        RulesDir = rulesDir;
        Note = note;
    }

    /// <summary>The save's game header.</summary>
    public GameSaveHeader Header { get; }

    /// <summary>The restored simulation (at the saved tick).</summary>
    public Simulation Sim { get; }

    /// <summary>The map.</summary>
    public MapData Map { get; }

    /// <summary>The AI driver with a fresh AI on every saved slot, or null if there is none.</summary>
    public AiDriver? Ai { get; }

    /// <summary>The restored script host (Mission mode), else null.</summary>
    public MissionHost? Host { get; }

    /// <summary>The data/rules directory the game was rebuilt from.</summary>
    public string RulesDir { get; }

    /// <summary>A line to tell the player about the load, or empty (for example that the AI restarts fresh).</summary>
    public string Note { get; }
}
