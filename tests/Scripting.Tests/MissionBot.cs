// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>
/// A scripted human player for mission tests (docs/missions/test_template.md). It sends the same commands the UI sends, one
/// <see cref="Simulation.Step(IReadOnlyList{Command})"/> at a time, so a test reads like a walkthrough of the mission.
/// Every call that runs ticks stops early when the game is over.
/// </summary>
internal sealed class MissionBot
{
    private readonly MissionSession _m;
    private readonly byte _player;
    private ushort _seq;

    /// <summary>A bot that plays <paramref name="player"/> (default: slot 0, the human) of <paramref name="mission"/>.</summary>
    public MissionBot(MissionSession mission, byte player = 0)
    {
        _m = mission;
        _player = player;
    }

    /// <summary>The simulation the bot plays.</summary>
    public Simulation Sim => _m.Sim;

    /// <summary>The mission script host (objectives, messages, errors, outcome).</summary>
    public MissionHost Host => _m.Host;

    /// <summary>Runs one tick with no command.</summary>
    public void Idle() => Sim.Step(TestData.NoCommands);

    /// <summary>Runs <paramref name="ticks"/> ticks with no command (stops when the game is over).</summary>
    public void Idle(int ticks)
    {
        for (int i = 0; i < ticks && !Sim.GameOver; i++) Idle();
    }

    /// <summary>Sends one raw command (and runs one tick).</summary>
    public void Send(CommandKind kind, EntityId subject, CellCoord cell, int arg, int arg2 = 0) =>
        Sim.Step(new[] { new Command(Sim.Tick, _player, _seq++, kind, subject, default, cell, arg, arg2) });

    /// <summary>Gives every mobile unit and soldier of the player the same order (Move, AttackMove, Guard...) and runs one tick.</summary>
    public void OrderAll(CommandKind kind, CellCoord cell)
    {
        Simulation s = Sim;
        var cmds = new List<Command>();
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == _player && !IsHarvester(s.Units.TypeIndex[i]))
                cmds.Add(new Command(s.Tick, _player, _seq++, kind, s.Units.Slots.IdOf(i), default, cell, (int)ActorKind.Vehicle));
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == _player)
                cmds.Add(new Command(s.Tick, _player, _seq++, kind, s.Infantry.Slots.IdOf(i), default, cell, (int)ActorKind.Infantry));
        s.Step(cmds);
    }

    /// <summary>Queues one item in a production queue (a structure, a unit...) by rules id. Fails the test on an unknown id.</summary>
    public void Train(string typeId)
    {
        (ProductionCategory cat, int index) = Lookup(typeId);
        Send(CommandKind.Build, default, default, (int)cat, index);
    }

    /// <summary>Tries to place a finished building with its top-left corner on <paramref name="cell"/>; true when it appeared.</summary>
    public bool TryPlace(string typeId, CellCoord cell)
    {
        int before = Count(typeId);
        (_, int index) = Lookup(typeId);
        Send(CommandKind.Place, default, cell, index);
        return Count(typeId) > before;
    }

    /// <summary>
    /// Queues a building and places it on the first candidate cell that works, checking every 10 ticks, for at most
    /// <paramref name="maxTicks"/> ticks. True when the building stands.
    /// </summary>
    public bool BuildAndPlace(string typeId, IReadOnlyList<CellCoord> candidates, int maxTicks = 3000)
    {
        int before = Count(typeId);
        Train(typeId);
        for (int t = 0; t < maxTicks && !Sim.GameOver; t++)
        {
            if (t % 10 == 0)
                foreach (CellCoord c in candidates)
                    if (TryPlace(typeId, c)) return true;
            Idle();
        }
        return Count(typeId) > before;
    }

    /// <summary>Own buildings and units of one rules id (any queue). Counts only the bot's player.</summary>
    public int Count(string typeId)
    {
        Simulation s = Sim;
        var r = s.Setup.Rules;
        int n = 0;
        int b = SimRules.IndexOf(r.Buildings, typeId, x => x.Id);
        if (b >= 0)
            for (int i = 1; i < s.Buildings.Capacity; i++)
                if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == _player && s.Buildings.TypeIndex[i] == b) n++;
        int v = SimRules.IndexOf(r.Vehicles, typeId, x => x.Id);
        if (v >= 0)
            for (int i = 1; i < s.Units.Capacity; i++)
                if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == _player && s.Units.TypeIndex[i] == v) n++;
        int f = SimRules.IndexOf(r.Infantry, typeId, x => x.Id);
        if (f >= 0)
            for (int i = 1; i < s.Infantry.Capacity; i++)
                if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == _player && s.Infantry.TypeIndex[i] == f) n++;
        return n;
    }

    /// <summary>The player's cash.</summary>
    public int Cash => Sim.Players.Cash[_player];

    /// <summary>True when the objective exists and is completed.</summary>
    public bool Done(string objectiveId) =>
        Host.Objectives.Any(o => o.Id == objectiveId && o.Status == ObjectiveStatus.Completed);

    /// <summary>
    /// Runs idle ticks until <paramref name="condition"/> holds, the game ends or the tick reaches <paramref name="tickLimit"/>
    /// (an absolute tick). Returns true if the condition held. <paramref name="every"/> is how many ticks pass between checks.
    /// </summary>
    public bool RunUntil(Func<bool> condition, int tickLimit, int every = 1)
    {
        while (Sim.Tick < tickLimit && !Sim.GameOver)
        {
            if (condition()) return true;
            Idle(every);
        }
        return condition();
    }

    /// <summary>Runs idle ticks until the objective is completed or the absolute tick limit; true when it completed.</summary>
    public bool RunUntilObjective(string objectiveId, int tickLimit) => RunUntil(() => Done(objectiveId), tickLimit, 5);

    /// <summary>A readable one-line state dump to use as an assertion message.</summary>
    public string Diagnostics() =>
        $"tick {Sim.Tick} outcome {Host.Outcome} objectives [{string.Join("; ", Host.Objectives.Select(o => o.Id + "=" + o.Status))}] "
        + $"cash {Cash} msgs [{string.Join(" | ", Host.Messages.Select(x => x.Tick + ":" + x.Text))}] errors [{string.Join("; ", Host.Errors)}]";

    private (ProductionCategory Category, int Index) Lookup(string typeId)
    {
        var r = Sim.Setup.Rules;
        int i = SimRules.IndexOf(r.Infantry, typeId, x => x.Id);
        if (i >= 0) return (ProductionCategory.Infantry, i);
        i = SimRules.IndexOf(r.Vehicles, typeId, x => x.Id);
        if (i >= 0) return (ProductionCategory.Vehicle, i);
        i = SimRules.IndexOf(r.Buildings, typeId, x => x.Id);
        if (i >= 0) return (r.Buildings[i].Category, i);
        throw new ArgumentException($"unknown rules id '{typeId}'", nameof(typeId));
    }

    private bool IsHarvester(int vehicleIndex) => Sim.Setup.Rules.Vehicles[vehicleIndex].HarvestCapacity > 0;
}
