// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Alerts;

/// <summary>
/// Turns one step of the game into <see cref="GameAlert"/>s. The "something happened" decision comes from the sound events
/// <see cref="AudioEventDeriver"/> already produces (under attack, unit ready, construction complete, low power); this class adds the
/// place and the kind (base / unit / harvester) by comparing the same two snapshots, plus building and unit losses and superweapon
/// ready / launched. Reads the views only.
/// </summary>
public sealed class AlertDeriver
{
    private readonly Func<UnitView, bool> _isHarvester;

    /// <summary>Creates the deriver. <paramref name="isHarvester"/> tells whether a unit is a harvester (null = none is).</summary>
    public AlertDeriver(Func<UnitView, bool>? isHarvester = null) => _isHarvester = isHarvester ?? (_ => false);

    /// <summary>Appends the alerts for <paramref name="f"/> to <paramref name="alerts"/>; <paramref name="audio"/> are the sound events derived from the same frame.</summary>
    public void Derive(AudioFrame f, IReadOnlyList<AudioEvent> audio, List<GameAlert> alerts)
    {
        SimSnapshot prev = f.Previous, cur = f.Current;
        byte me = f.LocalPlayer;
        int tick = cur.Tick;
        bool stepped = prev.Tick != cur.Tick;
        bool Has(AudioEventKind k) { foreach (AudioEvent e in audio) if (e.Kind == k) return true; return false; }

        if (Has(AudioEventKind.ConstructionComplete)) alerts.Add(new GameAlert(AlertKind.ConstructionComplete, tick));
        if (Has(AudioEventKind.LowPower)) alerts.Add(new GameAlert(AlertKind.LowPower, tick));
        if (Has(AudioEventKind.UpgradeComplete)) alerts.Add(new GameAlert(AlertKind.UpgradeComplete, tick));    // M13-15 (the sound already exists)
        if (Has(AudioEventKind.LowFunds)) alerts.Add(new GameAlert(AlertKind.ProductionBlocked, tick));         // M13-15: the stalled-queue event
        if (!stepped) return;

        IReadOnlyList<FxEvent> fx = cur.FxEvents ?? Array.Empty<FxEvent>();
        var prevUnits = new Dictionary<EntityId, UnitView>();
        foreach (UnitView u in prev.Units) prevUnits[u.Id] = u;
        var prevBuildings = new Dictionary<EntityId, BuildingView>();
        foreach (BuildingView b in prev.Buildings) prevBuildings[b.Id] = b;

        if (Has(AudioEventKind.UnitReady))
        {
            LeptonCoord? at = null;
            foreach (UnitView u in cur.Units)
            {
                if (u.Player == me && !u.Underground && !prevUnits.ContainsKey(u.Id)) { at = u.Position; break; }
            }

            alerts.Add(new GameAlert(AlertKind.UnitReady, tick, at));
        }

        if (Has(AudioEventKind.UnderAttack))
        {
            foreach (BuildingView b in cur.Buildings)
            {
                if (b.Player != me || b.Ghost || !prevBuildings.TryGetValue(b.Id, out BuildingView was) || b.Health >= was.Health) continue;
                alerts.Add(new GameAlert(AlertKind.BaseUnderAttack, tick, Centre(b)));
            }

            foreach (UnitView u in cur.Units)
            {
                if (u.Player != me || !prevUnits.TryGetValue(u.Id, out UnitView was) || u.Health >= was.Health) continue;
                alerts.Add(new GameAlert(_isHarvester(u) ? AlertKind.HarvesterUnderAttack : AlertKind.UnitUnderAttack, tick, u.Position));
            }
        }

        // Losses (a loss is also an attack, so it raises the attack alert for its place when the hit that killed it left no health drop to see).
        foreach (FxEvent e in fx)
        {
            if (e.Kind == FxKind.BuildingDestroyed)
            {
                foreach (BuildingView b in prev.Buildings)
                {
                    if (b.Player != me || b.Ghost || !InFootprint(b, e.Pos)) continue;
                    alerts.Add(new GameAlert(AlertKind.BuildingLost, tick, Centre(b)));
                    if (Has(AudioEventKind.UnderAttack)) alerts.Add(new GameAlert(AlertKind.BaseUnderAttack, tick, Centre(b)));
                    break;
                }
            }
            else if (e.Kind == FxKind.UnitDestroyed)
            {
                UnitView? dead = NearestOwn(prev, me, e.Pos);
                if (dead is not { } d) continue;
                alerts.Add(new GameAlert(AlertKind.UnitLost, tick, e.Pos));
                if (Has(AudioEventKind.UnderAttack)) alerts.Add(new GameAlert(_isHarvester(d) ? AlertKind.HarvesterUnderAttack : AlertKind.UnitUnderAttack, tick, e.Pos));
            }
        }

        // Superweapons: charged this step, or fired this step.
        foreach (SuperweaponView s in cur.Superweapons)
        {
            if (s.Owner != me) continue;
            bool wasReady = false;
            foreach (SuperweaponView p in prev.Superweapons) if (p.Building == s.Building && p.Ready) wasReady = true;
            if (s.Ready && !wasReady && prevBuildings.ContainsKey(s.Building)) alerts.Add(new GameAlert(AlertKind.SuperweaponReady, tick, BuildingPos(cur, s.Building)));
        }

        foreach (SuperweaponView p in prev.Superweapons)
        {
            if (p.Owner != me || !p.Ready) continue;
            bool stillReady = false;
            foreach (SuperweaponView s in cur.Superweapons) if (s.Building == p.Building && s.Ready) stillReady = true;
            if (stillReady) continue;
            foreach (FxEvent e in fx)
            {
                if (e.Kind != FxKind.Superweapon) continue;
                alerts.Add(new GameAlert(AlertKind.SuperweaponLaunched, tick, e.Pos));
                break;
            }
        }
    }

    private static LeptonCoord? BuildingPos(SimSnapshot s, EntityId id)
    {
        foreach (BuildingView b in s.Buildings) if (b.Id == id) return Centre(b);
        return null;
    }

    private static LeptonCoord Centre(BuildingView b) =>
        new((b.Cell.X * SimConstants.LeptonsPerCell) + (b.FootprintW * SimConstants.LeptonsPerCell / 2),
            (b.Cell.Y * SimConstants.LeptonsPerCell) + (b.FootprintH * SimConstants.LeptonsPerCell / 2));

    private static bool InFootprint(BuildingView b, LeptonCoord p)
    {
        int l = SimConstants.LeptonsPerCell;
        return p.X >= (b.Cell.X - 1) * l && p.X < (b.Cell.X + b.FootprintW + 1) * l
            && p.Y >= (b.Cell.Y - 1) * l && p.Y < (b.Cell.Y + b.FootprintH + 1) * l;
    }

    private static UnitView? NearestOwn(SimSnapshot prev, byte me, LeptonCoord p)
    {
        const long r = SimConstants.LeptonsPerCell;
        UnitView? best = null;
        long bestD = long.MaxValue;
        foreach (UnitView u in prev.Units)
        {
            if (u.Player != me) continue;
            long dx = u.Position.X - p.X, dy = u.Position.Y - p.Y;
            long d = dx * dx + dy * dy;
            if (d <= r * r && d < bestD) { best = u; bestD = d; }
        }

        return best;
    }
}
