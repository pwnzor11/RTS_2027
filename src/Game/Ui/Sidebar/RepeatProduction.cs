// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Repeat production on a cameo (M13-15, D-205): Alt+click turns a unit cameo into a loop, and the loop keeps one of that unit in its lane,
/// queueing another as soon as none is left (when the last one finished). It is front-end state only: the loop is a stream of ordinary
/// Build commands, so the sim, saves and replays are unchanged. Right-clicking the cameo (hold, then cancel) ends the loop. Only the unit
/// lanes loop; a structure waits to be placed, and an upgrade is bought once.
/// </summary>
public sealed class RepeatProduction
{
    /// <summary>Ticks to wait after queueing before another is queued (a command takes a tick or two to show in the lane).</summary>
    public const int ReissueTicks = 5;

    private readonly Dictionary<(ProductionCategory Category, int Type), int> _issuedAt = new();

    /// <summary>True for the categories that can loop.</summary>
    public static bool CanLoop(ProductionCategory category) =>
        category is ProductionCategory.Infantry or ProductionCategory.Vehicle or ProductionCategory.Aircraft;

    /// <summary>The loops that are on, in no particular order.</summary>
    public IReadOnlyCollection<(ProductionCategory Category, int Type)> Active => _issuedAt.Keys;

    /// <summary>True when the cameo loops.</summary>
    public bool IsOn(ProductionCategory category, int type) => _issuedAt.ContainsKey((category, type));

    /// <summary>Turns the loop of a cameo on or off. Returns the new state; a category that cannot loop stays off.</summary>
    public bool Toggle(ProductionCategory category, int type)
    {
        if (!CanLoop(category)) return false;
        if (_issuedAt.Remove((category, type))) return false;
        _issuedAt[(category, type)] = int.MinValue / 2;
        return true;
    }

    /// <summary>Ends the loop of a cameo (a no-op when it is off).</summary>
    public void Stop(ProductionCategory category, int type) => _issuedAt.Remove((category, type));

    /// <summary>Ends every loop (a new game).</summary>
    public void Clear() => _issuedAt.Clear();

    /// <summary>
    /// The Build commands due at <paramref name="tick"/>: for each loop whose type is in no queue slot of its lane, is still on offer and
    /// is not blocked (no factory, queue full, supply cap), one Build. A loop whose type is no longer on offer is dropped.
    /// </summary>
    public List<Command> Due(byte player, PlayerHud hud, int tick)
    {
        var due = new List<Command>();
        foreach ((ProductionCategory category, int type) in _issuedAt.Keys.ToList())
        {
            BuildOption? offer = null;
            foreach (BuildOption o in hud.Options) if (o.Category == category && o.TypeIndex == type) { offer = o; break; }
            if (offer is null) { _issuedAt.Remove((category, type)); continue; }
            if (offer.Value.Blocked != QueueBlockReason.None) continue;
            if (tick - _issuedAt[(category, type)] < ReissueTicks) continue;
            QueueView? lane = null;
            foreach (QueueView q in hud.Queues) if (q.Category == category) { lane = q; break; }
            if (lane is not null && lane.Items.Any(i => i.TypeIndex == type)) continue;
            Command? build = SidebarCommands.Build(player, offer.Value);
            if (build is not { } command) continue;
            due.Add(command);
            _issuedAt[(category, type)] = tick;
        }

        return due;
    }
}
