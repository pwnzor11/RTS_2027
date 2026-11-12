// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Ui.Alerts;

/// <summary>
/// Raises the "idle" alerts (M13-15, D-205): a harvester idle for <see cref="HarvesterTicks"/>, and a unit lane (infantry, vehicles,
/// aircraft) that was busy and then stood empty for <see cref="ProductionTicks"/> while a factory for it exists. Each fires once per
/// idle spell; the alert log rate-limits them again. Time is sim ticks; call <see cref="Observe"/> once per sim tick or per frame.
/// </summary>
public sealed class IdleWatch
{
    /// <summary>Ticks a harvester must stay idle before it is reported (15 s).</summary>
    public const int HarvesterTicks = 15 * SimConstants.TicksPerSecond;

    /// <summary>Ticks an emptied lane must stay empty before it is reported (10 s).</summary>
    public const int ProductionTicks = 10 * SimConstants.TicksPerSecond;

    private readonly Dictionary<EntityId, int> _harvesterSince = new();
    private readonly HashSet<EntityId> _harvesterReported = new();
    private readonly int[] _laneEmptySince = new int[IdleFinder.UnitLanes.Count];   // -1 = not empty (or never busy)
    private readonly bool[] _laneWasBusy = new bool[IdleFinder.UnitLanes.Count];
    private readonly bool[] _laneReported = new bool[IdleFinder.UnitLanes.Count];
    private readonly List<ViewActor> _idle = new();
    private int _lastTick = -1;

    /// <summary>Creates a watch with every lane marked "never busy".</summary>
    public IdleWatch() => Array.Fill(_laneEmptySince, -1);

    /// <summary>Forgets everything (a new game).</summary>
    public void Clear()
    {
        _harvesterSince.Clear();
        _harvesterReported.Clear();
        Array.Fill(_laneEmptySince, -1);
        Array.Clear(_laneWasBusy);
        Array.Clear(_laneReported);
        _lastTick = -1;
    }

    /// <summary>
    /// Looks at the state at <paramref name="tick"/> and appends the alerts that are due to <paramref name="alerts"/>. Does nothing when the
    /// tick has not moved or went backwards (then it forgets its state: a restart).
    /// </summary>
    public void Observe(int tick, IReadOnlyList<ViewActor> own, SimRules rules, PlayerHud hud, List<GameAlert> alerts)
    {
        if (tick == _lastTick) return;
        if (tick < _lastTick) Clear();
        _lastTick = tick;

        _idle.Clear();
        IdleFinder.Harvesters(own, rules, _idle);
        var stillIdle = new HashSet<EntityId>();
        foreach (ViewActor h in _idle)
        {
            stillIdle.Add(h.Id);
            if (!_harvesterSince.TryGetValue(h.Id, out int since)) { _harvesterSince[h.Id] = tick; continue; }
            if (tick - since >= HarvesterTicks && _harvesterReported.Add(h.Id)) alerts.Add(new GameAlert(AlertKind.HarvesterIdle, tick, h.Position));
        }

        foreach (EntityId id in _harvesterSince.Keys.ToList())
        {
            if (stillIdle.Contains(id)) continue;
            _harvesterSince.Remove(id);
            _harvesterReported.Remove(id);
        }

        for (int i = 0; i < IdleFinder.UnitLanes.Count; i++)
        {
            ProductionCategory lane = IdleFinder.UnitLanes[i];
            if (!IdleFinder.LaneIsEmpty(hud, lane))
            {
                _laneWasBusy[i] = true;
                _laneEmptySince[i] = -1;
                _laneReported[i] = false;
                continue;
            }

            if (!_laneWasBusy[i]) continue;
            if (_laneEmptySince[i] < 0) _laneEmptySince[i] = tick;
            if (tick - _laneEmptySince[i] < ProductionTicks || _laneReported[i]) continue;
            ViewActor? factory = FirstFactory(own, rules, lane);
            if (factory is null) continue;   // the factory is gone: nothing idles
            _laneReported[i] = true;
            alerts.Add(new GameAlert(AlertKind.ProductionIdle, tick, factory.Value.Position));
        }
    }

    private static ViewActor? FirstFactory(IReadOnlyList<ViewActor> own, SimRules rules, ProductionCategory lane)
    {
        foreach (ViewActor a in own)
        {
            if (a.Kind == ActorKind.Building && !a.Ghost && a.TypeIndex >= 0 && a.TypeIndex < rules.Buildings.Count && rules.Buildings[a.TypeIndex].Produces == lane) return a;
        }

        return null;
    }
}
