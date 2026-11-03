using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Combat part of Simulation. OWNER: M1-09 (combat).
public sealed partial class Simulation
{
    /// <summary>Facing error (of 256) within which a weapon mount counts as aimed.</summary>
    internal const int AimTolerance = 8;
    /// <summary>Ticks between target scans of an idle guard (mobile units and buildings).</summary>
    internal const int ScanIntervalTicks = 15;

    private CombatState _combat = null!;
    private ProjectileStore _projectiles = null!;

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

    /// <summary>True if the actor is alive and owned by a different player.</summary>
    public bool IsEnemy(byte player, ActorRef actor) => TryGetActorInfo(actor, out var info) && info.Owner != player;

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
        if (target.Kind != ActorKind.Building) { point = info.Position; return true; }
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

    /// <summary>Alive, health &gt; 0 and not owned by <paramref name="player"/>.</summary>
    internal bool IsValidTarget(byte player, ActorRef target) =>
        !target.Id.IsNone && TryGetActorInfo(target, out var info) && info.Health > 0 && info.Owner != player;

    /// <summary>True if the viewer currently sees the actor's cell (buildings: any footprint cell).</summary>
    internal bool IsTargetVisible(byte viewer, ActorRef target)
    {
        if (target.Kind > ActorKind.Building || !SlotsOf(target.Kind).IsAlive(target.Id)) return false;
        return IsTargetVisible(viewer, target.Kind, target.Id.Index);
    }

    private bool IsTargetVisible(byte viewer, ActorKind kind, int i)
    {
        if (kind != ActorKind.Building) return _vision.IsVisible(viewer, PositionAt(kind, i).ToCell());
        BuildingType bt = BuildingTypeAt(i);
        return FootprintVisible(viewer, _buildings.Cell[i], bt.FootprintW, bt.FootprintH);
    }

    /// <summary>Nearest visible enemy (vehicles, infantry, then buildings, slot order breaks ties) whose distance from
    /// <paramref name="from"/> lies in the weapon's [MinRange, Range]. Returns a ref with Id.IsNone if there is none.</summary>
    internal ActorRef AcquireTarget(byte player, LeptonCoord from, WeaponType w)
    {
        ActorRef best = default;
        long bestD = long.MaxValue;
        long maxD2 = (long)w.Range * w.Range, minD2 = (long)w.MinRange * w.MinRange;
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
            {
                if (!slots.IsIndexAlive(i) || PlayerAt(kind, i) == player || HealthAt(kind, i) <= 0) continue;
                LeptonCoord pt;
                if (kind == ActorKind.Building) { if (!TargetPoint(new ActorRef(kind, slots.IdOf(i)), from, out pt)) continue; }
                else pt = PositionAt(kind, i);
                long d = LeptonMath.DistanceSquared(from, pt);
                if (d < bestD && d <= maxD2 && d >= minD2 && IsTargetVisible(player, kind, i))
                { bestD = d; best = new ActorRef(kind, slots.IdOf(i)); }
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
        ref CombatEntry e = ref _combat.At(kind, i);
        if (e.ReloadTicks > 0) return false;
        byte owner = PlayerAt(kind, i);
        WeaponType w = RulesOf(owner).Weapons[weapon];
        if (w.ProjectileSpeed <= 0)
            Detonate(owner, w.Damage, w.Warhead, targetPoint, target, true);
        else
        {
            if (!_projectiles.HasFree) return false;
            _projectiles.Spawn(PositionAt(kind, i), target, targetPoint, w.ProjectileSpeed, w.Damage, w.Warhead, owner, weapon);
        }
        e.ReloadTicks = w.RateOfFire < 1 ? 1 : w.RateOfFire;
        return true;
    }

    // ---- damage -------------------------------------------------------------------------------

    /// <summary>Base damage after the warhead's armour table: damage × versus% / 100, minimum 1.</summary>
    public static int ComputeDamage(int damage, WarheadType warhead, ArmorClass armor)
    {
        int pct = (int)armor < warhead.VersusPercent.Count ? warhead.VersusPercent[(int)armor] : 100;
        int d = (int)((long)damage * pct / 100);
        return d < 1 ? 1 : d;
    }

    private void Hurt(ActorKind kind, int i, int damage, WarheadType wh, bool half)
    {
        int d = ComputeDamage(damage, wh, ArmorAt(kind, i));
        if (half) { d /= 2; if (d < 1) d = 1; }
        switch (kind)
        {
            case ActorKind.Vehicle: _units.Health[i] -= d; WakeOnHit(_units, kind, i); break;
            case ActorKind.Infantry: _infantry.Health[i] -= d; WakeOnHit(_infantry, kind, i); break;
            default: _buildings.Health[i] -= d; break;
        }
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
    /// projectile damages every eligible actor in the impact cell. Splash n: every eligible actor within n cells
    /// (Chebyshev; buildings by nearest footprint cell) takes full damage at distance 0 and half (min 1) otherwise.
    /// Eligible = not owned by <paramref name="owner"/>, unless the warhead HitsAllies.
    /// </summary>
    internal void Detonate(byte owner, int damage, int warheadIndex, LeptonCoord impact, ActorRef primary, bool instant)
    {
        WarheadType wh = RulesOf(owner).Warheads[warheadIndex];
        if (instant && wh.Splash <= 0)
        {
            if (TryGetActorInfo(primary, out var pi) && pi.Health > 0)
                Hurt(primary.Kind, primary.Id.Index, damage, wh, false);
            return;
        }
        int radius = wh.Splash < 0 ? 0 : wh.Splash;
        CellCoord ic = impact.ToCell();
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
            {
                if (!slots.IsIndexAlive(i) || HealthAt(kind, i) <= 0) continue;
                if (!wh.HitsAllies && PlayerAt(kind, i) == owner) continue;
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
                if (d <= radius) Hurt(kind, i, damage, wh, d > 0);
            }
        }
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
                if (WeaponOf(kind, i) < 0 || target.Kind > ActorKind.Building || !IsValidTarget(c.Player, target)) return;
                e.AttackTarget = target;
                Queue(store!, i, MissionKind.Attack, default, c.Target);
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
            byte owner = _buildings.Player[i];
            WeaponType w = RulesOf(owner).Weapons[weapon];
            LeptonCoord from = BuildingCentre(i);
            if (!e.AttackTarget.Id.IsNone)
            {
                bool keep = IsValidTarget(owner, e.AttackTarget) && InRange(from, e.AttackTarget, w.Range, w.MinRange)
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
                p.Free(id);   // free first: detonation never sees its own slot
                Detonate(owner, damage, warhead, dest, default, false);
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
    }
}
