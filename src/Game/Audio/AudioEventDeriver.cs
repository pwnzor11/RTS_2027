// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Audio;

/// <summary>
/// Turns the difference between two consecutive snapshots (and HUD states) into sound events. Needs no sim changes: it reads only
/// the public views. Keeps a little state (which orders and surfacing telegraphs it has already reported). Derived events:
/// explosions from fx, shell fire from new projectiles, small-arms hits and "under attack" from health drops, unit ready, building
/// placed, sold, construction complete, upgrade complete, low power, low funds, harvester unload (cash rising), tunnel enter,
/// surfacing rumble, radiation crackle, Blackout hum, and move acknowledgements from newly queued orders.
/// </summary>
public sealed class AudioEventDeriver
{
    // ASSUMED (M3-09): a splash of at least this many cells sounds as a large explosion.
    private const int LargeSplashCells = 3;
    // ASSUMED (M3-09): a health drop within this many cells of a detonation is its splash, not a small-arms hit.
    private const int SplashRadiusCells = 3;
    // ASSUMED (M3-09): a surfacing telegraph rumbles when first seen and again every this many ticks.
    private const int RumbleRepeatTicks = 30;
    private const int MaxNewProjectileEvents = 3;

    private readonly HashSet<(byte Player, ushort Sequence)> _seenOrders = new();

    /// <summary>Appends the events for <paramref name="f"/> to <paramref name="events"/>.</summary>
    public void Derive(AudioFrame f, List<AudioEvent> events)
    {
        SimSnapshot prev = f.Previous;
        SimSnapshot cur = f.Current;
        byte me = f.LocalPlayer;
        IReadOnlyList<FxEvent> fx = cur.FxEvents ?? Array.Empty<FxEvent>();
        bool stepped = cur.Tick != prev.Tick;

        // Explosions.
        var detonations = new List<LeptonCoord>();
        if (stepped)
        {
            foreach (FxEvent e in fx)
            {
                bool large = e.Kind == FxKind.BuildingDestroyed || (e.Kind == FxKind.Detonation && e.Size >= LargeSplashCells);
                events.Add(new AudioEvent(large ? AudioEventKind.ExplosionLarge : AudioEventKind.ExplosionSmall, e.Pos));
                if (e.Kind == FxKind.Detonation) detonations.Add(e.Pos);
            }

            // Shells: projectiles that exist now and did not before (count change plus the ones that just landed).
            int added = f.Projectiles.Count - f.PreviousProjectiles.Count + detonations.Count;
            for (int i = 0; i < Math.Min(added, MaxNewProjectileEvents); i++)
            {
                int index = f.Projectiles.Count - 1 - i;
                events.Add(new AudioEvent(AudioEventKind.FireShell, index >= 0 ? f.Projectiles[index] : null));
            }
        }

        // Health drops: small-arms hits, and "under attack" for the local player.
        var prevUnits = new Dictionary<EntityId, UnitView>();
        foreach (UnitView u in prev.Units) prevUnits[u.Id] = u;
        var prevBuildings = new Dictionary<EntityId, BuildingView>();
        foreach (BuildingView b in prev.Buildings) prevBuildings[b.Id] = b;

        bool attacked = false;
        if (stepped)
        {
            foreach (UnitView u in cur.Units)
            {
                if (!prevUnits.TryGetValue(u.Id, out UnitView before) || u.Health >= before.Health) continue;
                attacked |= u.Player == me;
                if (!NearAny(detonations, u.Position)) events.Add(new AudioEvent(AudioEventKind.FireBullet, u.Position));
            }

            foreach (BuildingView b in cur.Buildings)
            {
                if (b.Ghost || !prevBuildings.TryGetValue(b.Id, out BuildingView before) || b.Health >= before.Health) continue;
                attacked |= b.Player == me;
                LeptonCoord centre = BuildingCentre(b);
                if (!NearAny(detonations, centre)) events.Add(new AudioEvent(AudioEventKind.FireBullet, centre));
            }

            // A local actor that died this tick was attacked too.
            foreach (FxEvent e in fx)
            {
                if (e.Kind == FxKind.UnitDestroyed && LocalUnitWasAt(prev, me, e.Pos)) attacked = true;
                if (e.Kind == FxKind.BuildingDestroyed && LocalBuildingWasAt(prev, me, e.Pos)) attacked = true;
            }
        }

        if (attacked) events.Add(new AudioEvent(AudioEventKind.UnderAttack));

        // Own units and buildings that appeared; own buildings that went away without being destroyed.
        if (stepped)
        {
            var curUnitIds = new HashSet<EntityId>();
            foreach (UnitView u in cur.Units)
            {
                curUnitIds.Add(u.Id);
                if (u.Player == me && !u.Underground && !prevUnits.ContainsKey(u.Id)) events.Add(new AudioEvent(AudioEventKind.UnitReady));
                if (u.Underground && prevUnits.TryGetValue(u.Id, out UnitView was) && !was.Underground)
                {
                    events.Add(new AudioEvent(AudioEventKind.TunnelEnter, u.Position));
                }
            }

            var curBuildingIds = new HashSet<EntityId>();
            foreach (BuildingView b in cur.Buildings)
            {
                curBuildingIds.Add(b.Id);
                if (b.Player == me && !b.Ghost && !prevBuildings.ContainsKey(b.Id)) events.Add(new AudioEvent(AudioEventKind.BuildingPlaced));
            }

            bool sold = false;
            foreach (BuildingView b in prev.Buildings)
            {
                if (b.Player != me || b.Ghost || curBuildingIds.Contains(b.Id)) continue;
                if (!fx.Any(e => e.Kind == FxKind.BuildingDestroyed && InFootprint(b, e.Pos))) sold = true;
            }

            if (sold) events.Add(new AudioEvent(AudioEventKind.Sold));

            // Cash rising with no sale: a harvester unloaded (or a refund; ASSUMED).
            if (!sold && f.PreviousHud is { } ph && f.CurrentHud is { } ch && ch.Cash > ph.Cash) events.Add(new AudioEvent(AudioEventKind.HarvesterUnload));
        }

        // Surfacing telegraphs.
        foreach (TelegraphView t in cur.Telegraphs)
        {
            bool isNew = !prev.Telegraphs.Any(p => p.Cell == t.Cell && p.Owner == t.Owner && p.Kind == t.Kind);
            if (isNew || (t.TicksLeft > 0 && t.TicksLeft % RumbleRepeatTicks == 0 && stepped))
            {
                events.Add(new AudioEvent(AudioEventKind.SurfaceRumble, LeptonCoord.CenterOf(t.Cell)));
            }
        }

        // Radiation and Blackout under the player's own actors.
        if (cur.Ground.Count == cur.MapWidth * cur.MapHeight && cur.Ground.Count > 0)
        {
            bool hum = false;
            bool crackle = false;
            foreach (UnitView u in cur.Units)
            {
                if (u.Player != me) continue;
                CellCoord c = u.Position.ToCell();
                if (c.X < 0 || c.Y < 0 || c.X >= cur.MapWidth || c.Y >= cur.MapHeight) continue;
                byte g = cur.Ground[c.Y * cur.MapWidth + c.X];
                if (!crackle && (g & GroundLayers.RadiationMask) != 0)
                {
                    crackle = true;
                    events.Add(new AudioEvent(AudioEventKind.RadiationCrackle, u.Position));
                }

                hum |= (g & GroundLayers.Blackout) != 0;
            }

            foreach (BuildingView b in cur.Buildings)
            {
                if (b.Player != me || b.Ghost) continue;
                if (b.Cell.X >= 0 && b.Cell.Y >= 0 && b.Cell.X < cur.MapWidth && b.Cell.Y < cur.MapHeight
                    && (cur.Ground[b.Cell.Y * cur.MapWidth + b.Cell.X] & GroundLayers.Blackout) != 0)
                {
                    hum = true;
                }
            }

            if (hum) events.Add(new AudioEvent(AudioEventKind.BlackoutHum));
        }

        // HUD alerts.
        if (f.CurrentHud is { } hud)
        {
            if (hud.PowerUse > hud.PowerOut) events.Add(new AudioEvent(AudioEventKind.LowPower));
            if (f.PreviousHud is { } before)
            {
                foreach (QueueView q in hud.Queues)
                {
                    if (q.Category is not (ProductionCategory.Structure or ProductionCategory.Defence) || q.ReadyTypeIndex < 0) continue;
                    QueueView? old = before.Queues.FirstOrDefault(x => x.Category == q.Category);
                    if (old is null || old.ReadyTypeIndex < 0) events.Add(new AudioEvent(AudioEventKind.ConstructionComplete));
                }

                if ((hud.Upgrades & ~before.Upgrades) != 0) events.Add(new AudioEvent(AudioEventKind.UpgradeComplete));
            }

            if (hud.Queues.Any(q => q.Items.Any(i => i.Stalled))) events.Add(new AudioEvent(AudioEventKind.LowFunds));
        }

        // The player's own new orders.
        var present = new HashSet<(byte, ushort)>();
        bool ack = false;
        foreach (Command c in f.Pending)
        {
            if (c.Player != me) continue;
            present.Add((c.Player, c.Sequence));
            if (_seenOrders.Add((c.Player, c.Sequence)) && IsAckable(c.Kind)) ack = true;
        }

        _seenOrders.RemoveWhere(k => !present.Contains(k));
        if (ack) events.Add(new AudioEvent(AudioEventKind.MoveAck));
    }

    private static bool IsAckable(CommandKind k) =>
        k is CommandKind.Move or CommandKind.AttackMove or CommandKind.Attack or CommandKind.Harvest or CommandKind.Return
            or CommandKind.Enter or CommandKind.Guard or CommandKind.Scatter or CommandKind.Deploy;

    private static bool NearAny(List<LeptonCoord> points, LeptonCoord p)
    {
        const long r = (long)SplashRadiusCells * SimConstants.LeptonsPerCell;
        foreach (LeptonCoord d in points)
        {
            long dx = d.X - p.X;
            long dy = d.Y - p.Y;
            if (dx * dx + dy * dy <= r * r) return true;
        }

        return false;
    }

    private static LeptonCoord BuildingCentre(BuildingView b) =>
        new((b.Cell.X * SimConstants.LeptonsPerCell) + (b.FootprintW * SimConstants.LeptonsPerCell / 2),
            (b.Cell.Y * SimConstants.LeptonsPerCell) + (b.FootprintH * SimConstants.LeptonsPerCell / 2));

    private static bool InFootprint(BuildingView b, LeptonCoord p)
    {
        int l = SimConstants.LeptonsPerCell;
        return p.X >= (b.Cell.X - 1) * l && p.X < (b.Cell.X + b.FootprintW + 1) * l
            && p.Y >= (b.Cell.Y - 1) * l && p.Y < (b.Cell.Y + b.FootprintH + 1) * l;
    }

    private static bool LocalBuildingWasAt(SimSnapshot prev, byte me, LeptonCoord p) =>
        prev.Buildings.Any(b => b.Player == me && !b.Ghost && InFootprint(b, p));

    private static bool LocalUnitWasAt(SimSnapshot prev, byte me, LeptonCoord p)
    {
        const long r = SimConstants.LeptonsPerCell;
        return prev.Units.Any(u => u.Player == me && (long)(u.Position.X - p.X) * (u.Position.X - p.X) + (long)(u.Position.Y - p.Y) * (u.Position.Y - p.Y) <= r * r);
    }
}
