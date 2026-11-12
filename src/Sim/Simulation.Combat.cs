// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Combat part of Simulation. OWNER: M1-09 (combat).
//
// Per-tick combat phase order (D-065, simultaneous damage):
//   1. RunMissions: mission handlers in slot order acquire targets, aim and Fire. Acquisition, range and validity checks
//      read health as it stood at the START of the tick, because nothing changes Health before phase 4.
//   2. RunMovement.
//   3. RunCombat: reload countdown, building turrets (acquire, Fire), then projectile stepping and detonation.
//      Fire (instant weapons) and Detonate (shells, splash) only append (target, amount, source) to the pending-damage
//      buffer via Hurt; no Health value changes in phases 1-3, so a unit that is killed this tick still fires this tick.
//   4. ApplyPendingDamage (end of RunCombat): subtracts every queued hit from its target (subtraction commutes, so the
//      outcome does not depend on which slot hit first), wakes hit guards, and empties the buffer.
//   5. RunCleanup (later in the tick) removes everything with Health <= 0 and releases attackers' targets
//      (OnCombatActorRemoving). All deaths of a tick therefore resolve together.
// The buffer (_pendingDamage) is empty between ticks, so it is not part of the saved or hashed state.
public sealed partial class Simulation
{
    /// <summary>Facing error (of 256) within which a weapon mount counts as aimed.</summary>
    internal const int AimTolerance = 8;
    /// <summary>Ticks between target scans of an idle guard (mobile units and buildings).</summary>
    internal const int ScanIntervalTicks = 15;

    private CombatState _combat = null!;
    private ProjectileStore _projectiles = null!;

    // Hits queued by Hurt during a tick and applied together by ApplyPendingDamage; empty between ticks (not state).
    private PendingHit[] _pendingDamage = new PendingHit[256];
    private int _pendingCount;

    // Positions of mobile actors as they were when RunMissions began (valid while _useTickPos). Handlers move units as they
    // run, so without this a later slot would aim at, and range-check against, positions an earlier slot has already
    // changed this tick: a slot-order bias. Scratch, rewritten every tick, not state.
    private LeptonCoord[] _tickPosVeh = Array.Empty<LeptonCoord>();
    private LeptonCoord[] _tickPosInf = Array.Empty<LeptonCoord>();
    private bool _useTickPos;

    // This tick's effects events (cleared at the start of every Step); a renderer hint, not state (never hashed or saved).
    private readonly List<FxEvent> _fx = new();

    /// <summary>Detonations and deaths of the last Step (valid until the next Step). Not state.</summary>
    public IReadOnlyList<FxEvent> FxEvents => _fx;

    private readonly record struct PendingHit(ActorRef Target, int Amount, int Source, ActorRef Shooter);   // VET-2: Shooter = the actor that fired (none for death weapons, superweapons, radiation, storms)

    /// <summary>Per-actor combat data (reload, turret facing, attack target, guard point). Never mutate outside Step.</summary>
    public CombatState CombatData => _combat;

    /// <summary>Shells in flight. Never mutate outside Step.</summary>
    public ProjectileStore Projectiles => _projectiles;

    /// <summary>Extra state parts owned by combat, appended after "vision" in this order: combat, projectiles.</summary>
    private ISimStatePart[] CombatParts()
    {
        _combat = new CombatState(_units.Capacity, _infantry.Capacity, _buildings.Capacity);
        _projectiles = new ProjectileStore(Setup.Limits.Projectiles);
        return new ISimStatePart[] { _combat, _projectiles };
    }

    /// <summary>Record start-of-tick positions of vehicles and infantry for combat decisions (called at the start of RunMissions).</summary>
    private void SnapshotTickPositions()
    {
        if (_tickPosVeh.Length != _units.Capacity) _tickPosVeh = new LeptonCoord[_units.Capacity];
        if (_tickPosInf.Length != _infantry.Capacity) _tickPosInf = new LeptonCoord[_infantry.Capacity];
        Array.Copy(_units.Position, _tickPosVeh, _tickPosVeh.Length);
        Array.Copy(_infantry.Position, _tickPosInf, _tickPosInf.Length);
        _useTickPos = true;
    }

    // Where combat sees the actor: its start-of-tick position while a tick is running (buildings never move).
    private LeptonCoord CombatPositionAt(ActorKind kind, int i) =>
        !_useTickPos ? PositionAt(kind, i)
        : kind == ActorKind.Vehicle ? _tickPosVeh[i]
        : kind == ActorKind.Infantry ? _tickPosInf[i] : PositionAt(kind, i);

    // ---- lookups ------------------------------------------------------------------------------

    /// <summary>Facts about a live actor (vehicle, infantry or building). False if the reference is stale.</summary>
    public bool TryGetActorInfo(ActorRef actor, out ActorInfo info)
    {
        ActorKind k = actor.Kind;
        if (k > ActorKind.Building || !SlotsOf(k).IsAlive(actor.Id)) { info = default; return false; }
        int i = actor.Id.Index;
        info = new ActorInfo(k, PlayerAt(k, i), PositionAt(k, i), ArmorAt(k, i), HealthAt(k, i));
        return true;
    }

    /// <summary>True if the actor is alive and its owner is an Enemy of <paramref name="player"/> (not Self, Ally or Neutral).</summary>
    public bool IsEnemy(byte player, ActorRef actor) =>
        TryGetActorInfo(actor, out var info) && RelationOf(player, info.Owner) == Relation.Enemy;

    /// <summary>True if the target (buildings: nearest footprint cell centre) is within [minRange, range] leptons of <paramref name="from"/>.</summary>
    public bool InRange(LeptonCoord from, ActorRef target, int range, int minRange)
    {
        if (!TargetPoint(target, from, out LeptonCoord pt)) return false;
        int d = LeptonMath.Distance(from, pt);
        return d >= minRange && d <= range;
    }

    private LeptonCoord BuildingCentre(int i)
    {
        BuildingType bt = BuildingTypeAt(i);
        CellCoord c = _buildings.Cell[i];
        return new LeptonCoord((c.X << SimConstants.LeptonShift) + bt.FootprintW * (SimConstants.LeptonsPerCell / 2),
            (c.Y << SimConstants.LeptonShift) + bt.FootprintH * (SimConstants.LeptonsPerCell / 2));
    }

    /// <summary>Point shots at the actor aim for: its position, or for a building the footprint cell centre nearest <paramref name="from"/>.
    /// False if the actor is gone or already dead (health &lt;= 0).</summary>
    internal bool TargetPoint(ActorRef target, LeptonCoord from, out LeptonCoord point)
    {
        point = default;
        if (!TryGetActorInfo(target, out var info) || info.Health <= 0) return false;
        if (target.Kind != ActorKind.Building) { point = CombatPositionAt(target.Kind, target.Id.Index); return true; }
        int i = target.Id.Index;
        BuildingType bt = BuildingTypeAt(i);
        CellCoord tl = _buildings.Cell[i];
        long best = long.MaxValue;
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
            {
                LeptonCoord c = LeptonCoord.CenterOf(new CellCoord(tl.X + x, tl.Y + y));
                long d = LeptonMath.DistanceSquared(from, c);
                if (d < best) { best = d; point = c; }
            }
        return true;
    }

    /// <summary>Alive, health &gt; 0 and owned by an Enemy or Neutral player: what an explicit Attack order may target (D-054).</summary>
    internal bool IsValidTarget(byte player, ActorRef target) =>
        !target.Id.IsNone && TryGetActorInfo(target, out var info) && info.Health > 0 && CanAttackOwnedBy(player, info.Owner)
        && !IsUntargetableBy(player, target.Kind, target.Id.Index);   // M3-05: underground / hidden

    /// <summary>Alive, health &gt; 0 and owned by an Enemy: what auto-acquisition (Guard, AttackMove, turrets) may keep or pick.</summary>
    internal bool IsValidAutoTarget(byte player, ActorRef target) =>
        !target.Id.IsNone && TryGetActorInfo(target, out var info) && info.Health > 0 && RelationOf(player, info.Owner) == Relation.Enemy
        && !IsUntargetableBy(player, target.Kind, target.Id.Index);   // M3-05: underground / hidden

    /// <summary>True if the viewer currently sees the actor's cell (buildings: any footprint cell).</summary>
    internal bool IsTargetVisible(byte viewer, ActorRef target)
    {
        if (target.Kind > ActorKind.Building || !SlotsOf(target.Kind).IsAlive(target.Id)) return false;
        return IsTargetVisible(viewer, target.Kind, target.Id.Index);
    }

    private bool IsTargetVisible(byte viewer, ActorKind kind, int i)
    {
        if (kind != ActorKind.Building) return IsVisibleTo(viewer, CombatPositionAt(kind, i).ToCell());
        BuildingType bt = BuildingTypeAt(i);
        return FootprintVisible(viewer, _buildings.Cell[i], bt.FootprintW, bt.FootprintH);
    }

    /// <summary>Nearest visible enemy (vehicles, infantry, then buildings; equal distance: lower health, then slot order) whose distance from
    /// <paramref name="from"/> lies in the weapon's [MinRange, Range] (or [MinRange, <paramref name="searchRange"/>] when that is larger). Returns a ref with Id.IsNone if there is none.</summary>
    internal ActorRef AcquireTarget(byte player, LeptonCoord from, WeaponType w, int searchRange = 0)
    {
        ActorRef best = default;
        long bestD = long.MaxValue;
        int bestHp = int.MaxValue;
        int reach = Math.Max(w.Range, searchRange);   // M13-11 item 19: a melee unit searches out to its sight, not its reach
        long maxD2 = (long)reach * reach, minD2 = (long)w.MinRange * w.MinRange;
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
            {
                if (!slots.IsIndexAlive(i) || RelationOf(player, PlayerAt(kind, i)) != Relation.Enemy || HealthAt(kind, i) <= 0) continue;
                LeptonCoord pt;
                if (kind == ActorKind.Building) { if (!TargetPoint(new ActorRef(kind, slots.IdOf(i)), from, out pt)) continue; }
                else pt = CombatPositionAt(kind, i);
                long d = LeptonMath.DistanceSquared(from, pt);
                if (d > bestD || d > maxD2 || d < minD2) continue;
                // Equal distance: the weaker target (lower health) first, then kind and slot order. Health is the same for a
                // mirrored pair of targets, slot order is not.
                int hp = HealthAt(kind, i);
                if (d == bestD && hp >= bestHp) continue;
                if (!IsTargetVisible(player, kind, i)) continue;
                if (IsUntargetableBy(player, kind, i) || !WeaponCanTarget(w, kind, i)) continue;   // M3-05
                bestD = d; bestHp = hp; best = new ActorRef(kind, slots.IdOf(i));
            }
        }
        return best;
    }

    // ---- aiming and firing --------------------------------------------------------------------

    /// <summary>
    /// Turn the weapon mount of a vehicle or infantry slot toward <paramref name="to"/> by one tick of turning.
    /// Turreted vehicles turn the turret (independent of the hull); hull-mounted vehicles turn the hull in place;
    /// infantry turn instantly. True when the mount faces the point within <see cref="AimTolerance"/>.
    /// </summary>
    internal bool Aim(MobileStore store, ActorKind kind, int i, LeptonCoord to)
    {
        LeptonCoord pos = store.Position[i];
        byte desired = Facing.FromDelta(to.X - pos.X, to.Y - pos.Y);
        if (kind == ActorKind.Infantry) { store.Facing[i] = desired; return true; }
        VehicleType vt = RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]];
        if (vt.HasTurret)
        {
            ref CombatEntry e = ref _combat.At(kind, i);
            if (!e.TurretInit) { e.TurretInit = true; e.TurretFacing = store.Facing[i]; }
            e.TurretFacing = Facing.TurnToward(e.TurretFacing, desired, vt.TurretTurnRate);
            return Math.Abs(Facing.Difference(e.TurretFacing, desired)) <= AimTolerance;
        }
        byte next = Facing.TurnToward(store.Facing[i], desired, vt.TurnRate);
        store.Facing[i] = next;
        bool aimed = Math.Abs(Facing.Difference(next, desired)) <= AimTolerance;
        store.Status[i] = aimed ? MissionStatus.Waiting : MissionStatus.Turning;
        return aimed;
    }

    /// <summary>Fire the weapon if its reload is over. Instant weapons apply damage now; others spawn a projectile
    /// aimed at <paramref name="targetPoint"/>. Returns true if a shot was fired.</summary>
    internal bool Fire(ActorKind kind, int i, int weapon, ActorRef target, LeptonCoord targetPoint)
    {
        ActorLayer layer = _tunnels.LayerOf(kind, i);   // M3-03: diving and underground units cannot fire
        if (layer == ActorLayer.Diving || ActorLayers.IsUnderground(layer)) return false;
        ref CombatEntry e = ref _combat.At(kind, i);
        if (e.ReloadTicks > 0) return false;
        byte owner = PlayerAt(kind, i);
        WeaponType w = RulesOf(owner).Weapons[weapon];
        bool ammoBound = kind == ActorKind.Vehicle && RulesOf(owner).Vehicles[_units.TypeIndex[i]].Ammo > 0;   // D-156
        if (ammoBound && _naval.AmmoLeft[i] <= 0) return false;
        if (kind != ActorKind.Building && IsUnderground(kind, i)) return false;                       // M3-05: underground cannot fire
        if (!target.Id.IsNone && target.Kind <= ActorKind.Building && !WeaponCanTarget(w, target.Kind, target.Id.Index)) return false;   // M3-05: air vs ground
        // VET-2: the shooter rides along with the shot so the kill can be credited (mobile actors only: buildings never rank; none when veterancy is off)
        ActorRef shooter = kind != ActorKind.Building && VetRules.Enabled ? new ActorRef(kind, IdAt(kind, i)) : default;
        int rank = shooter.Id.IsNone ? 0 : RankOf(kind, i);   // VET-3: the shooter's rank at the moment of firing
        int damage = rank == 0 ? w.Damage : ScaleByRank(VetRules.DamagePermille, rank, w.Damage);
        if (w.ProjectileSpeed <= 0)
            Detonate(owner, damage, w.Warhead, targetPoint, target, true, shooter, w.Targets);
        else
        {
            if (!_projectiles.HasFree) return false;
            _projectiles.Spawn(PositionAt(kind, i), target, targetPoint, w.ProjectileSpeed, damage, w.Warhead, owner, weapon, shooter);
        }
        if (ammoBound) _naval.AmmoLeft[i]--;   // D-156: one round per shot
        e.ReloadTicks = w.RateOfFire < 1 ? 1 : w.RateOfFire;
        if (kind != ActorKind.Building) e.ReloadTicks = Math.Max(1, (int)((long)e.ReloadTicks * _effects.RofPermille(kind, i) / 1000));   // M3-06: aura rate of fire
        if (rank > 0) e.ReloadTicks = Math.Max(1, ScaleByRank(VetRules.ReloadPermille, rank, e.ReloadTicks));   // VET-3: faster reload
        if (!shooter.Id.IsNone) MarkCombat(kind, i);   // VET-3: self-heal waits after a shot
        if (kind == ActorKind.Vehicle) { _effects.VehicleStill[i] = 0; if (_naval.Submerged[i]) _naval.RevealLeft[i] = SubRevealTicks; }   // D-205 (SIM-RULES-04): firing always breaks stealth, whatever the reload; (LE-05): a dived sub that fires is seen
        else if (kind == ActorKind.Infantry) _effects.InfantryStill[i] = 0;
        if (kind == ActorKind.Vehicle && RulesOf(owner).Vehicles[_units.TypeIndex[i]].OneUse) RemoveMobileQuietly(kind, i);   // D-205 (LE-14): the Kite is spent by its bomb
        return true;
    }

    // ---- damage -------------------------------------------------------------------------------

    /// <summary>Base damage after the warhead's armour table: damage × versus% / 100, minimum 1.</summary>
    public static int ComputeDamage(int damage, WarheadType warhead, ArmorClass armor)
    {
        int pct = (int)armor < warhead.VersusPercent.Count ? warhead.VersusPercent[(int)armor] : 100;
        if (damage <= 0) return 0;   // D-205 (FAD-13): a damage-0 warhead deals 0, not the minimum 1
        int d = (int)((long)damage * pct / 100);
        return d < 1 ? 1 : d;
    }

    // Queue the hit; Health is untouched until ApplyPendingDamage (D-065). source = owner player, for diagnostics.
    private void Hurt(ActorKind kind, int i, int damage, WarheadType wh, bool half, byte source, ActorRef shooter = default)
    {
        int d = ComputeDamage(damage, wh, ArmorAt(kind, i));
        if (d <= 0) return;   // D-205 (FAD-13)
        if (kind == ActorKind.Building && wh.BonusVsEntrancePercent != 100 && BuildingTypeAt(i).Entrance != TunnelSize.None)
            d = Math.Max(1, (int)((long)d * wh.BonusVsEntrancePercent / 100));   // M4-05: bonus vs tunnel entrances
        if (wh.BonusVsIrradiatedPercent != 100 && IsIrradiatedTarget(kind, i)) d = Math.Max(1, (int)((long)d * wh.BonusVsIrradiatedPercent / 100));   // M3-05
        if (half) { d /= 2; if (d < 1) d = 1; }
        if (kind != ActorKind.Building)
        {
            int rank = RankOf(kind, i);   // VET-3: a ranked target takes less damage
            if (rank > 0) d = Math.Max(1, ScaleByRank(VetRules.TakenPermille, rank, d));
        }
        QueueHit(new ActorRef(kind, IdAt(kind, i)), d, source, shooter);
        if (kind == ActorKind.Building && wh.GarrisonDamagePercent > 0 && BuildingTypeAt(i).Garrison > 0)
        {
            // M4-05: a % of each hit on a garrisoned building also reaches every occupant (infantry in layer Garrisoned whose carrier
            // is this building). Slot order; the summed result does not depend on it.
            EntityId bid = _buildings.Slots.IdOf(i);
            int share = Math.Max(1, (int)((long)d * wh.GarrisonDamagePercent / 100));
            for (int s = 1; s < _infantry.Capacity; s++)
                if (_infantry.Slots.IsIndexAlive(s) && _infantry.Health[s] > 0 && _tunnels.InfantryLayer[s] == ActorLayer.Garrisoned && _tunnels.InfantryCarrier[s] == bid)
                    QueueHit(new ActorRef(ActorKind.Infantry, _infantry.Slots.IdOf(s)), share, source, shooter);
        }
    }

    private void QueueHit(ActorRef target, int d, byte source, ActorRef shooter)
    {
        if (_pendingCount == _pendingDamage.Length) Array.Resize(ref _pendingDamage, _pendingCount * 2);
        _pendingDamage[_pendingCount++] = new PendingHit(target, d, source, shooter);
    }

    /// <summary>Apply every queued hit and empty the buffer. Health only ever decreases by the sum of the hits, so the result
    /// does not depend on the order they arrived in (slot order of the shooters).</summary>
    private void ApplyPendingDamage()
    {
        for (int n = 0; n < _pendingCount; n++)
        {
            ref readonly PendingHit h = ref _pendingDamage[n];
            ActorKind kind = h.Target.Kind;
            int i = h.Target.Id.Index;
            if (!SlotsOf(kind).IsAlive(h.Target.Id)) continue;
            int before = HealthAt(kind, i);
            switch (kind)
            {
                case ActorKind.Vehicle: _units.Health[i] -= h.Amount; WakeOnHit(_units, kind, i); MarkCombat(kind, i); break;   // VET-3: MarkCombat
                case ActorKind.Infantry: _infantry.Health[i] -= h.Amount; WakeOnHit(_infantry, kind, i); MarkCombat(kind, i); break;
                default: _buildings.Health[i] -= h.Amount; break;
            }
            if (before > 0 && HealthAt(kind, i) <= 0)
            {
                OnLethalHit(h.Target, (byte)h.Source);   // M4-06: kill attribution
                OnSpoilsLethalHit(h.Target, (byte)h.Source);   // M6-05: Simulation.Raid.cs
                OnVeterancyKill(h.Shooter, h.Target, (byte)h.Source);   // VET-2: Simulation.Veterancy.cs
            }
        }
        _pendingCount = 0;
    }

    // A guard that is hit scans for targets on its next mission tick instead of waiting out its sleep.
    private void WakeOnHit(MobileStore store, ActorKind kind, int i)
    {
        if (store.Mission[i] != MissionKind.Guard) return;
        store.WakeTick[i] = 0;
        _combat.At(kind, i).ScanTick = 0;
    }

    /// <summary>
    /// Apply a warhead at an impact point. Splash 0: an instant weapon damages only <paramref name="primary"/>; a
    /// projectile damages ONE eligible actor of the impact cell: <paramref name="primary"/> (the shell's target) if it is still
    /// there, else the first eligible by kind and slot order (M2-19). Splash n: every eligible actor within n cells
    /// (Chebyshev; buildings by nearest footprint cell) takes full damage at distance 0 and half (min 1) otherwise.
    /// Eligible = not owned by <paramref name="owner"/> or its allies, unless the warhead HitsAllies (Neutral actors are always eligible).
    /// </summary>
    internal void Detonate(byte owner, int damage, int warheadIndex, LeptonCoord impact, ActorRef primary, bool instant, ActorRef shooter = default, WeaponTargets targets = WeaponTargets.Both)
    {
        WarheadType wh = RulesOf(owner).Warheads[warheadIndex];
        ApplyWarheadGround(wh, impact.ToCell(), wh.Splash < 0 ? 0 : wh.Splash);   // M3-05: contaminate / decontaminate / Blackout
        ApplyWarheadFoam(wh, impact.ToCell(), wh.Splash < 0 ? 0 : wh.Splash);     // M4-05: Quick-Set Foam
        if (instant && wh.Splash <= 0)
        {
            _fx.Add(new FxEvent(FxKind.InstantHit, impact, 1));   // M3-12: a hitscan hit shows its impact, like a shell's landing
            if (TryGetActorInfo(primary, out var pi) && pi.Health > 0)
                Hurt(primary.Kind, primary.Id.Index, damage, wh, false, owner, shooter);
            return;
        }
        int radius = wh.Splash < 0 ? 0 : wh.Splash;
        _fx.Add(new FxEvent(FxKind.Detonation, impact, Math.Max(1, radius)));
        CellCoord ic = impact.ToCell();
        bool single = wh.Splash <= 0;           // M2-19: a splash-0 shell hurts one actor of the impact cell
        ActorRef first = default;
        bool havePrimary = false;
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
            {
                if (!slots.IsIndexAlive(i) || HealthAt(kind, i) <= 0) continue;
                if (kind != ActorKind.Building)
                {
                    // M4-05: garrisoned soldiers are shielded by their building (it passes a share on); burrowed units are only
                    // hit by warheads with hits_burrowed.
                    ActorLayer layer = _tunnels.LayerOf(kind, i);
                    if (layer == ActorLayer.Garrisoned || SplashSparesSubmerged(kind, i, primary)) continue;   // M7-05
                    if (layer == ActorLayer.Passenger || layer == ActorLayer.Tunnel) continue;   // D-205 (SIM-RULES-05): riders, docked aircraft and tunnel transit are spared like every other area effect
                    // D-205 (FAD-06): splash respects air / ground (the shooter itself is exempt, so the Kite's own bomb still catches the Kite: LE-14 stays as it was). An anti-air burst (Air) hurts aircraft only; every other splash except Both spares aircraft.
                    if (targets != WeaponTargets.Both && !(shooter.Kind == kind && shooter.Id.Index == i && shooter.Id == IdAt(kind, i)) && IsAirUnit(kind, i) != (targets == WeaponTargets.Air)) continue;
                    if ((layer == ActorLayer.Burrowed || layer == ActorLayer.Surfacing) && !wh.HitsBurrowed) continue;
                }
                if (!wh.HitsAllies && IsFriendly(owner, PlayerAt(kind, i))) continue;
                if (kind == ActorKind.Building && targets == WeaponTargets.Air) continue;   // D-205 (FAD-06): flak never hurts buildings
                int d;
                if (kind == ActorKind.Building)
                {
                    BuildingType bt = BuildingTypeAt(i);
                    CellCoord tl = _buildings.Cell[i];
                    int dx = Math.Max(Math.Max(tl.X - ic.X, ic.X - (tl.X + bt.FootprintW - 1)), 0);
                    int dy = Math.Max(Math.Max(tl.Y - ic.Y, ic.Y - (tl.Y + bt.FootprintH - 1)), 0);
                    d = Math.Max(dx, dy);
                }
                else d = LeptonMath.ChebyshevCells(ic, PositionAt(kind, i).ToCell());
                if (d > radius) continue;
                if (!single) { Hurt(kind, i, damage, wh, d > 0, owner, shooter); continue; }
                var here = new ActorRef(kind, IdAt(kind, i));
                if (here == primary) havePrimary = true;
                if (first.Id.IsNone) first = here;
            }
        }
        if (!single) return;
        ActorRef victim = havePrimary ? primary : first;
        if (!victim.Id.IsNone) Hurt(victim.Kind, victim.Id.Index, damage, wh, false, owner, shooter);
    }

    // ---- commands -----------------------------------------------------------------------------

    /// <summary>Attack / AttackMove / Guard commands (payload layout in Command.cs).</summary>
    private void ApplyCombatCommand(Command c)
    {
        if (!OwnsSubject(c with { Arg = c.Arg & 0xFF }, out var store)) return;   // OwnsSubject reads the whole Arg as the subject kind
        var kind = (ActorKind)(c.Arg & 0xFF);
        int i = c.Subject.Index;
        ref CombatEntry e = ref _combat.At(kind, i);
        switch (c.Kind)
        {
            case CommandKind.Attack:
            {
                var target = new ActorRef((ActorKind)((c.Arg >> 8) & 0xFF), c.Target);
                if (kind == ActorKind.Vehicle) TryCarrierLaunch(c.Player, i, target);   // M7-09: Simulation.Carrier.cs
                if (WeaponOf(kind, i) < 0 || target.Kind > ActorKind.Building || !IsValidTarget(c.Player, target)) return;
                if (!WeaponCanTarget(RulesOf(c.Player).Weapons[WeaponOf(kind, i)], target.Kind, target.Id.Index)) return;   // M3-05
                e.AttackTarget = target;
                Queue(store!, i, MissionKind.Attack, default, c.Target);
                break;
            }
            case CommandKind.AttackGround:
            {
                // S19 (FAD-05): mobile units only; an unarmed unit, an anti-air-only or submerged-only weapon or an off-map cell is refused.
                if (kind > ActorKind.Infantry || !InMap(c.Cell) || WeaponOf(kind, i) < 0) return;
                WeaponTargets wt = RulesOf(c.Player).Weapons[WeaponOf(kind, i)].Targets;
                if (wt is WeaponTargets.Air or WeaponTargets.Submerged) return;
                e.AttackTarget = default;
                Queue(store!, i, MissionKind.AttackGround, c.Cell, default);
                break;
            }
            case CommandKind.AttackMove:
                if (!InMap(c.Cell)) return;
                e.GuardPoint = c.Cell; e.GuardPending = true; e.AttackTarget = default;
                Queue(store!, i, MissionKind.AttackMove, c.Cell, default);
                break;
            case CommandKind.Guard:
                if (InMap(c.Cell)) { e.GuardPoint = c.Cell; e.GuardPending = true; }
                e.AttackTarget = default;
                Queue(store!, i, MissionKind.Guard, c.Cell, default);
                break;
        }
    }

    // ---- per-tick system ----------------------------------------------------------------------

    /// <summary>Combat system: reload countdown, building guard/fire, then projectiles. Mobile units fire from their
    /// mission handlers (CombatMissions) earlier in the tick. Runs once per tick after movement.</summary>
    private void RunCombat()
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                ref CombatEntry e = ref _combat.At(kind, i);
                if (e.ReloadTicks > 0) e.ReloadTicks--;
            }
        }
        RunBuildingCombat();
        RunProjectiles();
        ApplyPendingDamage();
        _useTickPos = false;
    }

    private void RunBuildingCombat()
    {
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i)) continue;
            ref CombatEntry e = ref _combat.At(ActorKind.Building, i);
            ushort gen = _buildings.Slots.GenerationOf(i);
            if (e.Generation != gen) { e = default; e.Generation = gen; }
            int weapon = WeaponOf(ActorKind.Building, i);
            if (weapon < 0 || _buildings.Health[i] <= 0) continue;
            if (BuildingTypeAt(i).NeedsPower && PowerBand(_buildings.Player[i]) >= 1) continue;   // M2-08: unpowered defences hold fire
            byte owner = _buildings.Player[i];
            WeaponType w = RulesOf(owner).Weapons[weapon];
            LeptonCoord from = BuildingCentre(i);
            if (!e.AttackTarget.Id.IsNone)
            {
                bool keep = IsValidAutoTarget(owner, e.AttackTarget) && InRange(from, e.AttackTarget, w.Range, w.MinRange)
                    && IsTargetVisible(owner, e.AttackTarget);
                if (!keep) e.AttackTarget = default;
            }
            if (e.AttackTarget.Id.IsNone && Tick >= e.ScanTick)
            {
                e.AttackTarget = AcquireTarget(owner, from, w);
                e.ScanTick = Tick + ScanIntervalTicks;
            }
            if (e.AttackTarget.Id.IsNone || e.ReloadTicks > 0) continue;
            if (TargetPoint(e.AttackTarget, from, out LeptonCoord tp)) Fire(ActorKind.Building, i, weapon, e.AttackTarget, tp);
        }
    }

    private void RunProjectiles()
    {
        ProjectileStore p = _projectiles;
        for (int i = 1; i < p.Capacity; i++)
        {
            if (!p.Slots.IsIndexAlive(i)) continue;
            LeptonCoord pos = p.Position[i], dest = p.TargetPos[i];
            long dx = dest.X - pos.X, dy = dest.Y - pos.Y;
            long remaining = LeptonMath.ISqrt((ulong)(dx * dx + dy * dy));
            int speed = p.Speed[i];
            if (remaining <= speed)
            {
                EntityId id = p.Slots.IdOf(i);
                byte owner = p.Owner[i];
                int damage = p.Damage[i], warhead = p.Warhead[i];
                ActorRef target = p.Target[i], shooter = p.Shooter[i];
                WeaponTargets targets = RulesOf(owner).Weapons[p.Source[i]].Targets;
                p.Free(id);   // free first: detonation never sees its own slot
                Detonate(owner, damage, warhead, dest, target, false, shooter, targets);
                continue;
            }
            p.Position[i] = new LeptonCoord(pos.X + (int)RoundDiv(dx * speed, remaining), pos.Y + (int)RoundDiv(dy * speed, remaining));
        }
    }

    // Round-to-nearest (halves away from zero) integer division; d > 0.
    private static long RoundDiv(long n, long d) => (n * 2 + (n < 0 ? -d : d)) / (2 * d);

    /// <summary>Called before an actor is freed; clear anything targeting it.</summary>
    private void OnCombatActorRemoving(ActorKind kind, int index)
    {
        var dead = new ActorRef(kind, IdAt(kind, index));
        for (int k = 0; k < LiveKinds; k++)
        {
            int cap = CapacityOf((ActorKind)k);
            for (int i = 1; i < cap; i++)
            {
                ref CombatEntry e = ref _combat.At((ActorKind)k, i);
                if (e.AttackTarget == dead) e.AttackTarget = default;
            }
        }
        for (int i = 1; i < _projectiles.Capacity; i++)
            if (_projectiles.Target[i] == dead) _projectiles.Target[i] = default;
        _combat.At(kind, index) = default;
        _combat.ResetOrders(kind, index);   // S19 (M13-15): stance and waypoint queue die with the unit
    }
}
