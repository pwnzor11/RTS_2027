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
/// S22: with <see cref="AudioFrame.Rules"/> it also tells the faction of buildings and units, the class of a dead unit (infantry, vehicle,
/// ship, air), the sound class of the weapon that fired (shells from the projectile's weapon, instant hits from the nearest armed
/// enemy in range), water splashes, sonar contacts (an enemy submarine comes into view), the ship horn of a new ship, aircraft flyby and hover
/// (throttled per aircraft) and rig deploy / pack. Without rules it raises the generic events only, as before.
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
    // S22: at most this many hits per step look for the weapon that made them (the rest stay generic fire_bullet; the director's cooldown merges them anyway).
    private const int MaxAttributedHits = 8;
    // S22: seconds between two sounds of one aircraft.
    private const int FlybyEverySeconds = 6;
    private const int HoverEverySeconds = 4;
    private const int AirMemoryLimit = 512;

    private readonly HashSet<(byte Player, ushort Sequence)> _seenOrders = new();
    private readonly Dictionary<EntityId, int> _airNextTick = new();
    private readonly List<(LeptonCoord Pos, byte Player, int Weapon)> _armed = new();

    /// <summary>Appends the events for <paramref name="f"/> to <paramref name="events"/>.</summary>
    public void Derive(AudioFrame f, List<AudioEvent> events)
    {
        SimSnapshot prev = f.Previous;
        SimSnapshot cur = f.Current;
        byte me = f.LocalPlayer;
        IReadOnlyList<FxEvent> fx = cur.FxEvents ?? Array.Empty<FxEvent>();
        bool stepped = cur.Tick != prev.Tick;
        AudioRules? rules = f.Rules;

        var prevUnits = new Dictionary<EntityId, UnitView>();
        foreach (UnitView u in prev.Units) prevUnits[u.Id] = u;
        var prevBuildings = new Dictionary<EntityId, BuildingView>();
        foreach (BuildingView b in prev.Buildings) prevBuildings[b.Id] = b;
        var curUnitIds = new HashSet<EntityId>();
        foreach (UnitView u in cur.Units) curUnitIds.Add(u.Id);
        var curBuildingIds = new HashSet<EntityId>();
        foreach (BuildingView b in cur.Buildings) curBuildingIds.Add(b.Id);

        // S22: rigs that unfolded or packed this step, so their building / unit is not reported as placed, sold or ready.
        HashSet<EntityId>? rigUnits = null;
        HashSet<EntityId>? rigBuildings = null;
        if (stepped && rules is not null) DetectRigs(f, prevUnits, prevBuildings, curUnitIds, curBuildingIds, events, ref rigUnits, ref rigBuildings);

        // Explosions.
        var detonations = new List<LeptonCoord>();
        if (stepped)
        {
            foreach (FxEvent e in fx)
            {
                if (e.Kind == FxKind.InstantHit) continue;   // M3-12: small-arms hits sound as FireBullet below
                if (rules is not null && e.Kind == FxKind.UnitDestroyed)
                {
                    events.Add(UnitDeath(rules, prev, curUnitIds, e.Pos));
                    continue;
                }

                if (rules is not null && e.Kind == FxKind.BuildingDestroyed)
                {
                    events.Add(BuildingDeath(rules, prev, curBuildingIds, e.Pos));
                    continue;
                }

                bool large = e.Kind == FxKind.BuildingDestroyed || (e.Kind == FxKind.Detonation && e.Size >= LargeSplashCells);
                if (e.Kind == FxKind.Detonation)
                {
                    detonations.Add(e.Pos);
                    if (IsOpenWater(prev, cur, e.Pos))
                    {
                        events.Add(new AudioEvent(AudioEventKind.WaterImpact, e.Pos, large ? "large" : null));
                        continue;
                    }

                    if (rules is not null && BomberNear(rules, cur, e.Pos))
                    {
                        events.Add(new AudioEvent(AudioEventKind.ExplosionLarge, e.Pos, WeaponClasses.Bomb));
                        continue;
                    }
                }

                events.Add(new AudioEvent(large ? AudioEventKind.ExplosionLarge : AudioEventKind.ExplosionSmall, e.Pos));
            }

            // Shells: projectiles that exist now and did not before (count change plus the ones that just landed).
            int added = f.Projectiles.Count - f.PreviousProjectiles.Count + detonations.Count;
            int fresh = f.Projectiles.Count - f.PreviousProjectiles.Count;   // S22: of those, the ones really in the list now
            for (int i = 0; i < Math.Min(added, MaxNewProjectileEvents); i++)
            {
                int index = f.Projectiles.Count - 1 - i;
                LeptonCoord? at = index >= 0 ? f.Projectiles[index] : null;
                if (rules is not null && i < fresh && index >= 0 && f.ProjectileWeapons.Count == f.Projectiles.Count)
                {
                    int weapon = f.ProjectileWeapons[index];
                    string? cls = rules.WeaponClass(weapon);
                    if (WeaponClasses.MakesNoShotSound(cls)) continue;
                    events.Add(new AudioEvent(WeaponClasses.FamilyOf(cls) ?? AudioEventKind.FireShell, at, cls, rules.WeaponFaction(weapon)));
                    continue;
                }

                events.Add(new AudioEvent(AudioEventKind.FireShell, at));
            }
        }

        // Health drops: small-arms hits, and "under attack" for the local player.
        int hitsLooked = 0;

        bool attacked = false;
        if (stepped)
        {
            foreach (UnitView u in cur.Units)
            {
                if (!prevUnits.TryGetValue(u.Id, out UnitView before) || u.Health >= before.Health) continue;
                attacked |= u.Player == me;
                if (!NearAny(detonations, u.Position) && TryHitEvent(f, u.Position, u.Player, hitsLooked++, out AudioEvent hit)) events.Add(hit);
            }

            foreach (BuildingView b in cur.Buildings)
            {
                if (b.Ghost || !prevBuildings.TryGetValue(b.Id, out BuildingView before) || b.Health >= before.Health) continue;
                attacked |= b.Player == me;
                LeptonCoord centre = BuildingCentre(b);
                if (!NearAny(detonations, centre) && TryHitEvent(f, centre, b.Player, hitsLooked++, out AudioEvent hit)) events.Add(hit);
            }

            // A local actor that died this tick was attacked too.
            foreach (FxEvent e in fx)
            {
                if (e.Kind == FxKind.UnitDestroyed && LocalUnitWasAt(prev, me, e.Pos)) attacked = true;
                if (e.Kind == FxKind.BuildingDestroyed && LocalBuildingWasAt(prev, me, e.Pos)) attacked = true;
            }
        }

        if (attacked) events.Add(new AudioEvent(AudioEventKind.UnderAttack));
        if (f.Promotions.Count > 0) events.Add(new AudioEvent(AudioEventKind.Promotion));   // VET-5

        // Own units and buildings that appeared; own buildings that went away without being destroyed.
        if (stepped)
        {
            string? hudFaction = rules is not null && f.CurrentHud is { } hudNow ? AudioRules.FactionKey(hudNow.Faction) : null;
            foreach (UnitView u in cur.Units)
            {
                bool isNew = !prevUnits.ContainsKey(u.Id);
                if (u.Player == me && !u.Underground && isNew && rigUnits?.Contains(u.Id) != true) events.Add(UnitReady(rules, u, hudFaction));
                if (u.Underground && prevUnits.TryGetValue(u.Id, out UnitView was) && !was.Underground)
                {
                    events.Add(new AudioEvent(AudioEventKind.TunnelEnter, u.Position));
                }

                if (rules is not null && isNew && u.Player != me && rules.Unit(u.Kind, u.TypeIndex).Submarine)
                {
                    events.Add(new AudioEvent(AudioEventKind.Sonar, u.Position));   // S22: an enemy submarine came into view
                }
            }

            foreach (BuildingView b in cur.Buildings)
            {
                if (b.Player == me && !b.Ghost && !prevBuildings.ContainsKey(b.Id) && rigBuildings?.Contains(b.Id) != true)
                {
                    string? faction = rules is null ? null : rules.Building(b.TypeIndex).Faction ?? hudFaction;
                    events.Add(new AudioEvent(AudioEventKind.BuildingPlaced, null, null, faction));
                }
            }

            bool sold = false;
            foreach (BuildingView b in prev.Buildings)
            {
                if (b.Player != me || b.Ghost || curBuildingIds.Contains(b.Id) || rigBuildings?.Contains(b.Id) == true) continue;
                if (!fx.Any(e => e.Kind == FxKind.BuildingDestroyed && InFootprint(b, e.Pos))) sold = true;
            }

            if (sold) events.Add(new AudioEvent(AudioEventKind.Sold));

            // Cash rising with no sale: a harvester unloaded (or a refund; ASSUMED).
            if (!sold && f.PreviousHud is { } ph && f.CurrentHud is { } ch && ch.Cash > ph.Cash) events.Add(new AudioEvent(AudioEventKind.HarvesterUnload));

            if (rules is not null) DeriveAir(f, rules, prevUnits, events);
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
                    if (old is null || old.ReadyTypeIndex < 0)
                    {
                        events.Add(new AudioEvent(AudioEventKind.ConstructionComplete, null, null, rules is null ? null : AudioRules.FactionKey(hud.Faction)));
                    }
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

    private static AudioEvent UnitReady(AudioRules? rules, UnitView u, string? hudFaction)
    {
        if (rules is null) return new AudioEvent(AudioEventKind.UnitReady);
        AudioUnitInfo info = rules.Unit(u.Kind, u.TypeIndex);
        // A new ship sounds its horn; with no horn sound the event falls back to unit_ready.ship, then unit_ready.
        return new AudioEvent(info.Class == "ship" ? AudioEventKind.ShipHorn : AudioEventKind.UnitReady, null, info.Class, info.Faction ?? hudFaction);
    }

    // The unit that vanished at pos this step: the nearest previous unit that is gone now, within a cell.
    private static AudioEvent UnitDeath(AudioRules rules, SimSnapshot prev, HashSet<EntityId> curIds, LeptonCoord pos)
    {
        const long r = SimConstants.LeptonsPerCell;
        long best = r * r + 1;
        AudioUnitInfo? found = null;
        foreach (UnitView u in prev.Units)
        {
            if (curIds.Contains(u.Id)) continue;
            long dx = u.Position.X - pos.X;
            long dy = u.Position.Y - pos.Y;
            long d = (dx * dx) + (dy * dy);
            if (d < best)
            {
                best = d;
                found = rules.Unit(u.Kind, u.TypeIndex);
            }
        }

        return found is { } info
            ? new AudioEvent(AudioEventKind.UnitDied, pos, info.Class, info.Faction)
            : new AudioEvent(AudioEventKind.UnitDied, pos);
    }

    private static AudioEvent BuildingDeath(AudioRules rules, SimSnapshot prev, HashSet<EntityId> curIds, LeptonCoord pos)
    {
        foreach (BuildingView b in prev.Buildings)
        {
            if (b.Ghost || curIds.Contains(b.Id) || !InFootprint(b, pos)) continue;
            return new AudioEvent(AudioEventKind.BuildingDestroyed, pos, null, rules.Building(b.TypeIndex).Faction);
        }

        return new AudioEvent(AudioEventKind.BuildingDestroyed, pos);
    }

    // A detonation on a water cell with nothing standing within a cell of it: a miss into the sea. A ship that is hit is an explosion.
    private static bool IsOpenWater(SimSnapshot prev, SimSnapshot cur, LeptonCoord pos)
    {
        if (cur.Land.Count != cur.MapWidth * cur.MapHeight || cur.Land.Count == 0) return false;
        CellCoord c = pos.ToCell();
        if (c.X < 0 || c.Y < 0 || c.X >= cur.MapWidth || c.Y >= cur.MapHeight) return false;
        if (cur.Land[(c.Y * cur.MapWidth) + c.X] != LandType.Water) return false;
        const long r = SimConstants.LeptonsPerCell;
        foreach (UnitView u in prev.Units)
        {
            if (Within(u.Position, pos, r)) return false;
        }

        foreach (UnitView u in cur.Units)
        {
            if (Within(u.Position, pos, r)) return false;
        }

        foreach (BuildingView b in cur.Buildings)
        {
            if (!b.Ghost && InFootprint(b, pos)) return false;
        }

        return true;
    }

    // An aircraft with a bomb weapon within 4 cells of the blast: the detonation is a bomb.
    private static bool BomberNear(AudioRules rules, SimSnapshot cur, LeptonCoord pos)
    {
        const long r = 4L * SimConstants.LeptonsPerCell;
        foreach (UnitView u in cur.Units)
        {
            if (u.Kind != UnitKind.Vehicle || !Within(u.Position, pos, r)) continue;
            AudioUnitInfo info = rules.Unit(u.Kind, u.TypeIndex);
            if (info.Locomotion == Locomotion.Air && info.PrimaryWeapon >= 0 && rules.WeaponClass(info.PrimaryWeapon) == WeaponClasses.Bomb) return true;
        }

        return false;
    }

    private static bool Within(LeptonCoord a, LeptonCoord b, long r)
    {
        long dx = a.X - b.X;
        long dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy) <= r * r;
    }

    // The sound for a health drop that no detonation explains: an instant (hitscan) weapon hit. With rules, the nearest armed enemy whose instant
    // weapon reaches the victim names the weapon class and faction; with no shooter in view (or no rules) it is the generic fire_bullet.
    private bool TryHitEvent(AudioFrame f, LeptonCoord victim, byte victimPlayer, int looked, out AudioEvent hit)
    {
        hit = new AudioEvent(AudioEventKind.FireBullet, victim);
        AudioRules? rules = f.Rules;
        if (rules is null || looked >= MaxAttributedHits) return true;
        if (looked == 0) CollectArmed(f, rules);
        long best = long.MaxValue;
        int weapon = -1;
        foreach ((LeptonCoord pos, byte player, int w) in _armed)
        {
            if (player == victimPlayer) continue;
            long dx = pos.X - victim.X;
            long dy = pos.Y - victim.Y;
            long d = (dx * dx) + (dy * dy);
            long reach = rules.WeaponRange(w) + SimConstants.LeptonsPerCell;
            if (d <= reach * reach && d < best)
            {
                best = d;
                weapon = w;
            }
        }

        if (weapon < 0) return true;
        string? cls = rules.WeaponClass(weapon);
        if (WeaponClasses.MakesNoShotSound(cls)) return false;
        hit = new AudioEvent(WeaponClasses.FamilyOf(cls) ?? AudioEventKind.FireBullet, victim, cls, rules.WeaponFaction(weapon));
        return true;
    }

    private void CollectArmed(AudioFrame f, AudioRules rules)
    {
        _armed.Clear();
        foreach (UnitView u in f.Current.Units)
        {
            int w = rules.Unit(u.Kind, u.TypeIndex).PrimaryWeapon;
            if (w >= 0 && rules.IsInstant(w)) _armed.Add((u.Position, u.Player, w));
        }

        foreach (BuildingView b in f.Current.Buildings)
        {
            if (b.Ghost) continue;
            int w = rules.Building(b.TypeIndex).PrimaryWeapon;
            if (w >= 0 && rules.IsInstant(w)) _armed.Add((BuildingCentre(b), b.Player, w));
        }
    }

    // A rig unfolds (a vehicle with unpacks_into vanishes as its building appears) or packs (a building with packs_into vanishes as the vehicle appears).
    private static void DetectRigs(AudioFrame f, Dictionary<EntityId, UnitView> prevUnits, Dictionary<EntityId, BuildingView> prevBuildings,
        HashSet<EntityId> curUnitIds, HashSet<EntityId> curBuildingIds, List<AudioEvent> events,
        ref HashSet<EntityId>? rigUnits, ref HashSet<EntityId>? rigBuildings)
    {
        AudioRules rules = f.Rules!;
        SimSnapshot cur = f.Current;
        foreach (BuildingView b in cur.Buildings)
        {
            if (b.Ghost || prevBuildings.ContainsKey(b.Id)) continue;
            LeptonCoord centre = BuildingCentre(b);
            foreach (UnitView u in f.Previous.Units)
            {
                if (u.Kind != UnitKind.Vehicle || curUnitIds.Contains(u.Id)) continue;
                if (rules.Unit(u.Kind, u.TypeIndex).UnpacksInto != b.TypeIndex || !InFootprint(b, u.Position)) continue;
                (rigBuildings ??= new()).Add(b.Id);
                events.Add(new AudioEvent(AudioEventKind.RigDeploy, centre, null, rules.Building(b.TypeIndex).Faction));
                break;
            }
        }

        foreach (BuildingView b in f.Previous.Buildings)
        {
            if (b.Ghost || curBuildingIds.Contains(b.Id)) continue;
            int packsInto = rules.Building(b.TypeIndex).PacksInto;
            if (packsInto < 0) continue;
            foreach (UnitView u in cur.Units)
            {
                if (u.Kind != UnitKind.Vehicle || u.TypeIndex != packsInto || prevUnits.ContainsKey(u.Id) || !InFootprint(b, u.Position)) continue;
                (rigUnits ??= new()).Add(u.Id);
                (rigBuildings ??= new()).Add(b.Id);
                events.Add(new AudioEvent(AudioEventKind.RigPack, BuildingCentre(b), null, rules.Building(b.TypeIndex).Faction));
                break;
            }
        }
    }

    // Aircraft sound now and then, each on its own timer: jets and bombers as a flyby while they move, rotor aircraft as a hover.
    private void DeriveAir(AudioFrame f, AudioRules rules, Dictionary<EntityId, UnitView> prevUnits, List<AudioEvent> events)
    {
        int tick = f.Current.Tick;
        if (_airNextTick.Count > AirMemoryLimit) _airNextTick.Clear();
        foreach (UnitView u in f.Current.Units)
        {
            if (u.Kind != UnitKind.Vehicle || u.Carried) continue;
            AudioUnitInfo info = rules.Unit(u.Kind, u.TypeIndex);
            if (AudioRules.AirSound(info) is not { } sound) continue;
            int every = (sound == AudioEventKind.AirFlyby ? FlybyEverySeconds : HoverEverySeconds) * SimConstants.TicksPerSecond;
            if (!_airNextTick.TryGetValue(u.Id, out int next))
            {
                _airNextTick[u.Id] = tick + ((u.Id.Index * 17) % every);   // stagger the first sound of a fleet
                continue;
            }

            if (tick < next) continue;
            bool moved = prevUnits.TryGetValue(u.Id, out UnitView was) && was.Position != u.Position;
            if (sound == AudioEventKind.AirFlyby && !moved) continue;
            _airNextTick[u.Id] = tick + every;
            events.Add(new AudioEvent(sound, u.Position, null, info.Faction));
        }
    }

    private static bool IsAckable(CommandKind k) =>
        k is CommandKind.Move or CommandKind.AttackMove or CommandKind.Attack or CommandKind.Harvest or CommandKind.Return
            or CommandKind.Enter or CommandKind.Guard or CommandKind.Scatter or CommandKind.Deploy or CommandKind.QueueOrder;

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
