// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Unit production for the Recruit tier: one item at a time per lane, looping a fixed mix (infantry: riflemen with a
/// rocketeer now and then; vehicles: jeeps first, then tanks), only while cash covers the unit plus the credits the
/// build order still needs. Sets each new barracks/factory's rally point in front of the base, toward the map centre.
/// </summary>
internal sealed class ProductionManager
{
    private static readonly string[] InfantryMix = ["auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rocketeer"];
    private static readonly string[] VehicleMix = ["auth_jeep", "auth_jeep", "auth_tank", "auth_tank", "auth_jeep", "auth_tank"];

    private int[] _inf = Array.Empty<int>(), _veh = Array.Empty<int>();
    private int _infIdx, _vehIdx;
    private readonly HashSet<ulong> _rallied = new();

    /// <summary>Units queued so far (infantry + vehicles).</summary>
    public int Queued { get; private set; }

    /// <summary>Rally cell in front of the base (set after the first Think with a yard).</summary>
    public CellCoord Rally { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, int reserve)
    {
        SimRules rules = k.Rules;
        if (_inf.Length == 0)
        {
            _inf = Resolve(InfantryMix, rules.Infantry, i => i.Id);
            _veh = Resolve(VehicleMix, rules.Vehicles, v => v.Id);
        }
        if (!k.HaveYard) return;
        Rally = RallyCell(k);

        foreach (ViewActor b in k.Buildings)
        {
            if (rules.Buildings[b.TypeIndex].Produces == null) continue;
            ulong key = ((ulong)b.Id.Index << 16) | b.Id.Generation;
            if (!_rallied.Add(key)) continue;
            sink.Rally(b.Id, Rally);
        }

        // Vehicles first; infantry leaves the next vehicle's price in reserve so cheap riflemen cannot starve the factory.
        Lane(k, sink, reserve, ProductionCategory.Vehicle, _veh, ref _vehIdx, t => rules.Vehicles[t].Cost);
        int vehicleReserve = k.Queue(ProductionCategory.Vehicle).Items.Count == 0 && k.Buildable(ProductionCategory.Vehicle, _veh[_vehIdx % _veh.Length])
            ? rules.Vehicles[_veh[_vehIdx % _veh.Length]].Cost : 0;
        Lane(k, sink, reserve + vehicleReserve, ProductionCategory.Infantry, _inf, ref _infIdx, t => rules.Infantry[t].Cost);
    }

    private void Lane(RecruitKnowledge k, ICommandSink sink, int reserve, ProductionCategory cat, int[] mix, ref int idx,
        Func<int, int> cost)
    {
        if (k.Queue(cat).Items.Count > 0) return;
        int type = mix[idx % mix.Length];
        if (!k.Buildable(cat, type)) return;
        if (k.Hud.Cash + k.Hud.Stored < cost(type) + reserve) return;
        sink.Build(cat, type);
        idx++;
        Queued++;
    }

    private static int[] Resolve<T>(string[] ids, IReadOnlyList<T> list, Func<T, string> idOf)
    {
        var r = new int[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            r[i] = SimRules.IndexOf(list, ids[i], idOf);
            if (r[i] < 0) throw new InvalidOperationException("unit mix names unknown type '" + ids[i] + "'");
        }
        return r;
    }

    /// <summary>A passable cell about 7 cells from the yard toward the map centre.</summary>
    private static CellCoord RallyCell(RecruitKnowledge k)
    {
        var view = k.View;
        int cx = k.Yard.X + 1, cy = k.Yard.Y + 1;
        int dx = Math.Sign(view.MapWidth / 2 - cx), dy = Math.Sign(view.MapHeight / 2 - cy);
        for (int d = 7; d >= 2; d--)
        {
            var c = new CellCoord(Math.Clamp(cx + dx * d, 1, view.MapWidth - 2), Math.Clamp(cy + dy * d, 1, view.MapHeight - 2));
            if (view.IsPassable(c, Locomotion.Tracked)) return c;
        }
        return new CellCoord(cx, cy);
    }
}
