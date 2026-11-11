// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Garrison;
using Rts.Sim.Missions;
using Rts.Sim.Pathing;
using Rts.Sim.Production;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Production sim (M2-08): queues, prerequisites, cost drain, placement, rally, sell, repair, win rule.
// State lives in ProductionState; money goes through Spend/Refund (Simulation.Economy.cs).
public sealed partial class Simulation
{
    private const int MaxExitSearchRing = 8;

    // ---- queries ---------------------------------------------------------------------------------

    /// <summary>Cost, build time and prerequisites of a type in a category as <paramref name="player"/> sees it
    /// (<see cref="RulesOf"/>: cost and build-time upgrades apply); false if the category/index is not buildable. M4-04: Aircraft
    /// takes a vehicle index whose locomotion is Air, and the Vehicle category refuses Air types.</summary>
    private bool TryTypeInfo(byte player, ProductionCategory category, int typeIndex, out int cost, out int buildTime,
        out IReadOnlyList<string> prerequisites)
    {
        cost = 0; buildTime = 0; prerequisites = Array.Empty<string>();
        SimRules rules = RulesOf(player);
        switch (category)
        {
            case ProductionCategory.Infantry:
                if (rules.Infantry == null || typeIndex < 0 || typeIndex >= rules.Infantry.Count) return false;
                (cost, buildTime, prerequisites) = (rules.Infantry[typeIndex].Cost, rules.Infantry[typeIndex].BuildTime, rules.Infantry[typeIndex].Prerequisites);
                return true;
            case ProductionCategory.Vehicle:
            case ProductionCategory.Aircraft:
                if (typeIndex < 0 || typeIndex >= rules.Vehicles.Count) return false;
                VehicleType vt = rules.Vehicles[typeIndex];
                if ((vt.Locomotion == Locomotion.Air) != (category == ProductionCategory.Aircraft)) return false;
                if (!vt.Buildable) return false;   // D-205 (FACTION-LE-04): tender, Command Ship and the packed rig form are never offered
                (cost, buildTime, prerequisites) = (vt.Cost, vt.BuildTime, vt.Prerequisites);
                return true;
            case ProductionCategory.Structure:
            case ProductionCategory.Defence:
                if (rules.Buildings == null || typeIndex < 0 || typeIndex >= rules.Buildings.Count) return false;
                BuildingType bt = rules.Buildings[typeIndex];
                if (bt.Category != category || !bt.Buildable) return false;
                (cost, buildTime, prerequisites) = (bt.Cost, bt.BuildTime, bt.Prerequisites);
                return true;
            default:
                return false;
        }
    }

    /// <summary>True if the player owns an alive building whose id or provides list contains <paramref name="id"/>.</summary>
    private bool OwnsProvider(byte player, string id)
    {
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player) continue;
            BuildingType bt = BuildingTypeAt(i);
            if (string.Equals(bt.Id, id, StringComparison.Ordinal)) return true;
            for (int k = 0; k < bt.Provides.Count; k++)
                if (string.Equals(bt.Provides[k], id, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Every entry is met: an upgrade id needs that upgrade researched (M4-04), any other entry an alive own building whose
    /// id or provides list names it. Tier sugar entries are provides names, so losing the gate re-locks the tier (Q-M4-e, option A).</summary>
    private bool HasPrerequisites(byte player, IReadOnlyList<string> prerequisites) => UpgradePrerequisitesMet(player, prerequisites);

    private static bool IsUnitCategory(ProductionCategory c) =>
        c == ProductionCategory.Infantry || c == ProductionCategory.Vehicle || c == ProductionCategory.Aircraft;

    /// <summary>Number of alive buildings of the player that produce this unit category.</summary>
    private int CountFactories(byte player, ProductionCategory category)
    {
        int n = 0;
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && BuildingTypeAt(i).Produces == category
                && _rigs.PackTicksLeft[i] == 0) n++;   // M6-03: a packing rig does not produce
        return n;
    }

    /// <summary>The factory units of a category leave from: the chosen primary if alive, else the lowest-slot factory. None if no factory.</summary>
    private EntityId PrimaryFactoryOf(byte player, ProductionCategory category)
    {
        EntityId chosen = _production.Primary[ProductionState.Lane(player, category)];
        if (!chosen.IsNone && _buildings.Slots.IsAlive(chosen) && _buildings.Player[chosen.Index] == player
            && BuildingTypeAt(chosen.Index).Produces == category && _rigs.PackTicksLeft[chosen.Index] == 0) return chosen;   // M6-03
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && BuildingTypeAt(i).Produces == category
                && _rigs.PackTicksLeft[i] == 0)   // M6-03
                return _buildings.Slots.IdOf(i);
        return EntityId.None;
    }

    /// <summary>
    /// True if the type exists in the category and every prerequisite is provided by an alive own building (D-066: the sidebar
    /// lists every unlocked type). Says nothing about queue room or factories; see <see cref="QueueBlockReason"/>.
    /// </summary>
    public bool IsUnlocked(byte player, ProductionCategory category, int typeIndex)
    {
        if (player >= _players.Count || _players.IsNeutral(player)) return false;
        if (!TryTypeInfo(player, category, typeIndex, out _, out _, out var prereq)) return false;
        if (UnlockFilter != null && !UnlockFilter(player, category, typeIndex)) return false;   // M2-15: mission tech gating
        return HasPrerequisites(player, prereq);
    }

    /// <summary>
    /// Optional extra gate on <see cref="IsUnlocked"/> (M2-15): when set and it returns false for (player, category, type index) the type
    /// is locked whatever the prerequisites say. Set by a mission extension in <see cref="ISimExtension.OnSetup"/>; it must read only
    /// hashed state. Not saved.
    /// </summary>
    public Func<byte, ProductionCategory, int, bool>? UnlockFilter { get; set; }

    /// <summary>
    /// Why a Build command for this type would not be queued now, ignoring prerequisites (see <see cref="IsUnlocked"/>): the player is
    /// defeated, the type does not exist, a unit category has no factory, a finished structure is waiting to be placed, or the queue is
    /// full; <see cref="QueueBlockReason.None"/> when it would be accepted.
    /// </summary>
    public QueueBlockReason QueueBlockReason(byte player, ProductionCategory category, int typeIndex)
    {
        if (player >= _players.Count || _players.IsNeutral(player) || _players.Defeated[player]) return Core.QueueBlockReason.Defeated;
        if (!TryTypeInfo(player, category, typeIndex, out _, out _, out _)) return Core.QueueBlockReason.NotBuildable;
        int lane = ProductionState.Lane(player, category);
        if (IsUnitCategory(category) && CountFactories(player, category) == 0) return Core.QueueBlockReason.NoFactory;
        if (!IsUnitCategory(category) && _production.Ready[lane] >= 0) return Core.QueueBlockReason.StructureWaiting;
        if (_production.QueueCount[lane] >= _production.MaxQueue || _production.QueueCount[lane] >= Setup.Rules.Economy.MaxQueue)
            return Core.QueueBlockReason.QueueFull;
        if (SupplyBlocksEnqueue(player, category, typeIndex)) return Core.QueueBlockReason.SupplyCap;   // SUP-1
        return Core.QueueBlockReason.None;
    }

    /// <summary>
    /// Whether a Build command for this type would be accepted now: <see cref="IsUnlocked"/> and no <see cref="QueueBlockReason"/>.
    /// </summary>
    public bool CanBuild(byte player, ProductionCategory category, int typeIndex) =>
        IsUnlocked(player, category, typeIndex) && QueueBlockReason(player, category, typeIndex) == Core.QueueBlockReason.None;

    // ---- commands --------------------------------------------------------------------------------

    private void ApplyBuild(Command c)
    {
        if (c.Arg == (int)ProductionCategory.Upgrade) { ApplyBuildUpgrade(c); return; }   // M3: Simulation.Upgrades.cs
        if (c.Arg < 0 || c.Arg > (int)ProductionCategory.Defence) return;
        var category = (ProductionCategory)c.Arg;
        if (!CanBuild(c.Player, category, c.Arg2)) return;
        int lane = ProductionState.Lane(c.Player, category);
        int n = _production.QueueCount[lane];
        _production.QueueType[lane * _production.MaxQueue + n] = c.Arg2;
        _production.QueueCount[lane] = n + 1;
    }

    private void ApplyCancel(Command c)
    {
        if (c.Arg == (int)ProductionCategory.Upgrade) { ApplyCancelUpgrade(c); return; }   // M3: Simulation.Upgrades.cs
        if (_players.IsNeutral(c.Player) || c.Arg < 0 || c.Arg > (int)ProductionCategory.Defence) return;
        int lane = ProductionState.Lane(c.Player, (ProductionCategory)c.Arg);
        if (c.Arg2 == ReadyCancelPosition) { CancelReady(c.Player, (ProductionCategory)c.Arg, lane); return; }   // M13-06
        if (c.Arg2 < 0 || c.Arg2 >= _production.QueueCount[lane]) return;
        if (c.Arg2 == 0) Refund(c.Player, _production.Spent[lane]);   // only the front item has spent anything
        PopQueue(lane, c.Arg2);
    }

    /// <summary>M13-06: <c>Command.Arg2</c> of a Cancel that targets the finished structure waiting for placement (not a queue position).
    /// Queue position 0 is always the front of the queue, never the ready item.</summary>
    public const int ReadyCancelPosition = -1;

    /// <summary>M13-06: cancel the finished structure of a lane with a full refund of its cost, freeing the lane.</summary>
    private void CancelReady(byte player, ProductionCategory category, int lane)
    {
        int type = _production.Ready[lane];
        if (type < 0 || IsUnitCategory(category)) return;
        if (TryTypeInfo(player, category, type, out int cost, out _, out _)) Refund(player, cost);
        _production.Ready[lane] = -1;
    }

    /// <summary>S15: Hold. Arg = category, Arg2 = 1 hold / 0 resume the front item. Only a lane with a queued item can be held.</summary>
    private void ApplyHold(Command c)
    {
        if (_players.IsNeutral(c.Player) || c.Arg < 0 || c.Arg > (int)ProductionCategory.Upgrade) return;
        int lane = ProductionState.Lane(c.Player, (ProductionCategory)c.Arg);
        if (_production.QueueCount[lane] == 0) return;
        bool hold = c.Arg2 != 0;
        _production.Held[lane] = hold;
        if (hold) _production.Stalled[lane] = false;
    }

    /// <summary>Remove the item at a queue position; removing the front resets the progress.</summary>
    private void PopQueue(int lane, int position)
    {
        int q = _production.MaxQueue, n = _production.QueueCount[lane];
        for (int k = position; k < n - 1; k++) _production.QueueType[lane * q + k] = _production.QueueType[lane * q + k + 1];
        _production.QueueType[lane * q + n - 1] = 0;
        _production.QueueCount[lane] = n - 1;
        if (position == 0)
        {
            _production.Spent[lane] = 0; _production.Ticks[lane] = 0; _production.Total[lane] = 0;
            _production.Stalled[lane] = false;
            _production.Held[lane] = false;
        }
    }

    private void ApplyPlace(Command c)
    {
        if (_players.IsNeutral(c.Player) || c.Arg < 0 || c.Arg >= BuildingTypeCount) return;
        BuildingType bt = Setup.Rules.Buildings[c.Arg];
        int lane = ProductionState.Lane(c.Player, bt.Category);
        EntityId id;
        if (_production.Ready[lane] == c.Arg)
        {
            if (CanPlace(c.Player, c.Arg, c.Cell) != PlacementResult.Ok || !WithinBuildRadius(c.Player, bt, c.Cell)) return;
            id = PlaceBuilding(c.Player, c.Arg, c.Cell);
            if (id.IsNone) return;
            _production.Ready[lane] = -1;
        }
        else if (Setup.AllowDebug)
        {
            // Debug/test sims may place without a built item (no radius, no cost): the pre-M2 free placement.
            id = PlaceBuilding(c.Player, c.Arg, c.Cell);
            if (id.IsNone) return;
        }
        else return;
        _production.RepairGen[id.Index] = 0; _production.RepairFrac[id.Index] = 0;
        OnStatsBuilt(c.Player, ActorKind.Building);   // M4-06
        if (bt.FreeUnit >= 0 && bt.FreeUnit < Setup.Rules.Vehicles.Count)
            SpawnProducedVehicle(c.Player, bt.FreeUnit, c.Cell, bt, false, default);
    }

    /// <summary>
    /// D-062 build radius: the footprint at <paramref name="topLeft"/> must lie within Economy.BuildRadius cells (Chebyshev,
    /// nearest cell to nearest cell; a touching building is at distance 1) of an alive own non-wall building.
    /// </summary>
    private bool WithinBuildRadius(byte player, BuildingType bt, CellCoord topLeft)
    {
        int radius = Setup.Rules.Economy.BuildRadius;
        int ax0 = topLeft.X, ay0 = topLeft.Y, ax1 = ax0 + bt.FootprintW - 1, ay1 = ay0 + bt.FootprintH - 1;
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player) continue;
            BuildingType other = BuildingTypeAt(i);
            if (other.Wall || other.Gate) continue;   // integration: gates are wall pieces (M3-04)
            CellCoord oc = _buildings.Cell[i];
            int bx1 = oc.X + other.FootprintW - 1, by1 = oc.Y + other.FootprintH - 1;
            int dx = Math.Max(0, Math.Max(ax0 - bx1, oc.X - ax1)), dy = Math.Max(0, Math.Max(ay0 - by1, oc.Y - ay1));
            if (Math.Max(dx, dy) <= radius) return true;
        }
        return false;
    }

    private void ApplySell(Command c)
    {
        if (!_buildings.Slots.IsAlive(c.Subject) || _buildings.Player[c.Subject.Index] != c.Player) return;
        BuildingType bt = BuildingTypeAt(c.Subject.Index);
        Refund(c.Player, (int)((long)Setup.Rules.Economy.SellRefundPermille * bt.Cost / 1000));
        _production.RepairGen[c.Subject.Index] = 0; _production.RepairFrac[c.Subject.Index] = 0;
        RemoveBuilding(c.Subject);
    }

    private void ApplyRepair(Command c)
    {
        if (!_buildings.Slots.IsAlive(c.Subject) || _buildings.Player[c.Subject.Index] != c.Player) return;
        int i = c.Subject.Index;
        if (_production.RepairGen[i] != 0) { _production.RepairGen[i] = 0; _production.RepairFrac[i] = 0; }
        else _production.RepairGen[i] = _buildings.Slots.GenerationOf(i) + 1;
    }

    private void ApplyRally(Command c)
    {
        if (!_buildings.Slots.IsAlive(c.Subject) || _buildings.Player[c.Subject.Index] != c.Player || !InMap(c.Cell)) return;
        ProductionCategory? produces = BuildingTypeAt(c.Subject.Index).Produces;
        if (produces == null) return;
        int lane = ProductionState.Lane(c.Player, produces.Value);
        _production.Primary[lane] = c.Subject;
        _production.RallySet[lane] = true;
        _production.Rally[lane] = c.Cell;
    }

    // ---- per-tick production ---------------------------------------------------------------------

    private void RunProductionTick()
    {
        // Note who owns a base before cleanup can remove it (a building killed this tick still counts as "ever had one").
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && !(BuildingTypeAt(i).Wall || BuildingTypeAt(i).Gate)) _production.EverHadBase[_buildings.Player[i]] = true;
        for (int p = 0; p < _players.Count; p++)
        {
            var player = (byte)p;
            if (_players.IsNeutral(player) || _players.Defeated[p]) continue;
            for (int k = 0; k <= (int)ProductionCategory.Defence; k++)
            {
                var category = (ProductionCategory)k;
                int lane = ProductionState.Lane(p, category);
                if (_production.QueueCount[lane] == 0 || _production.Held[lane]) continue;   // S15: a held item neither progresses nor pays
                if (!IsUnitCategory(category) && _production.Ready[lane] >= 0) continue;   // wait for the placement
                if (!IsUnitCategory(category) && RigLanePaused(player, category)) continue;   // M6-03: rig packing or packed
                int factories = 1;
                if (IsUnitCategory(category))
                {
                    factories = CountFactories(player, category);
                    if (factories == 0) continue;
                }
                AdvanceLane(player, category, lane, factories);
            }
        }
        RunRepairs();
    }

    /// <summary>
    /// Build ticks of an item: base = SimRules.BuildTicks(cost, buildTime); then
    /// <c>total = base × slowdown / 1000 × 2 / (1 + factories)</c> (done as one integer expression, minimum 1), where slowdown is 1000 at
    /// full power and LowPowerSlowdownPermille[band - 1] otherwise, and <c>factories</c> is the number of alive factories of that unit
    /// category (1 for structures): one factory = normal speed, two = 2/3 of the time, three = 1/2, and so on.
    /// </summary>
    private int ProductionTotalTicks(byte player, int cost, int buildTime, int factories)
    {
        EconomyRules eco = Setup.Rules.Economy;
        long baseTicks = SimRules.BuildTicks(cost, buildTime, eco);
        int band = PowerBand(player);
        long slowdown = band > 0 && band <= eco.LowPowerSlowdownPermille.Count ? eco.LowPowerSlowdownPermille[band - 1] : 1000;
        return (int)Math.Max(1, baseTicks * slowdown * 2 / (1000L * (1 + factories)));
    }

    private void AdvanceLane(byte player, ProductionCategory category, int lane, int factories)
    {
        ProductionState s = _production;
        int type = s.QueueType[lane * s.MaxQueue];
        if (!TryTypeInfo(player, category, type, out int cost, out int buildTime, out _)) { PopQueue(lane, 0); return; }
        // SUP-1: a unit that does not fit under the supply cap pauses the lane: no spending, no progress, no refund; a finished unit
        // waits here until it fits (the same lane position as a blocked exit).
        if (IsUnitCategory(category) && SupplyEnabled && !SupplyFits(player, SupplyOfType(category, type))) return;
        int total = ProductionTotalTicks(player, cost, buildTime, factories);
        if (s.Total[lane] != total)
        {
            // Power or factory count changed mid-build: keep the completed fraction.
            if (s.Total[lane] > 0) s.Ticks[lane] = (int)((long)s.Ticks[lane] * total / s.Total[lane]);
            s.Total[lane] = total;
        }
        if (s.Ticks[lane] < total)
        {
            // Drain credits proportionally: cumulative cost due after this tick is cost × (ticks+1) / total.
            int want = (int)((long)cost * (s.Ticks[lane] + 1) / total);
            int due = Math.Max(0, want - s.Spent[lane]);
            int paid = Spend(player, due);
            s.Spent[lane] += paid;
            if (paid < due) { s.Stalled[lane] = true; return; }
            s.Stalled[lane] = false;
            s.Ticks[lane]++;
        }
        if (s.Ticks[lane] < total) return;
        if (!CompleteItem(player, category, type)) { s.Stalled[lane] = true; return; }   // no free exit cell: retry next tick
        PopQueue(lane, 0);
        if (!IsUnitCategory(category)) s.Ready[lane] = type;
    }

    /// <summary>Finish the front item. Structures and defences just become ready; units spawn at the primary factory's exit.</summary>
    private bool CompleteItem(byte player, ProductionCategory category, int type)
    {
        if (!IsUnitCategory(category)) return true;
        EntityId factory = category == ProductionCategory.Vehicle && IsNavalVehicle(player, type)
            ? ShipFactoryOf(player, type) : PrimaryFactoryOf(player, category);
        if (factory.IsNone) return false;
        BuildingType fbt = BuildingTypeAt(factory.Index);
        CellCoord topLeft = _buildings.Cell[factory.Index];
        int lane = ProductionState.Lane(player, category);
        CellCoord? rally = _production.RallySet[lane] ? _production.Rally[lane] : null;
        if (category == ProductionCategory.Aircraft && TryBuildIntoHangar(player, type, rally)) return true;   // M7-09
        if (category == ProductionCategory.Infantry)
            return SpawnProducedInfantry(player, type, topLeft, fbt, rally);
        if (category == ProductionCategory.Vehicle || category == ProductionCategory.Aircraft)   // M4-04: aircraft leave like vehicles
            return SpawnProducedVehicle(player, type, topLeft, fbt, true, rally);
        return false;
    }

    private bool IsNavalVehicle(byte player, int type) => MoveClass.IsNaval(MoveClass.Of(RulesOf(player).Vehicles[type]));

    /// <summary>
    /// Simfu-2: ships share the "vehicle" lane with land factories, so a ship leaves only from a factory whose exit cell or footprint
    /// holds a cell of its move class (a Shipyard or Hover Yard on the shore): the lane's primary if eligible, else the lowest-slot
    /// eligible factory. None when no factory qualifies (the item waits). Land units never come here and pick the primary as before.
    /// </summary>
    private EntityId ShipFactoryOf(byte player, int type)
    {
        Locomotion cls = MoveClass.Of(RulesOf(player).Vehicles[type]);
        EntityId primary = PrimaryFactoryOf(player, ProductionCategory.Vehicle);
        if (!primary.IsNone && FactoryReaches(primary.Index, cls)) return primary;
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && BuildingTypeAt(i).Produces == ProductionCategory.Vehicle
                && _rigs.PackTicksLeft[i] == 0 && FactoryReaches(i, cls))
                return _buildings.Slots.IdOf(i);
        return EntityId.None;
    }

    private bool FactoryReaches(int slot, Locomotion cls)
    {
        BuildingType fb = BuildingTypeAt(slot);
        CellCoord tl = _buildings.Cell[slot];
        CellCoord rel = OrientedOffset(fb.Exit, fb.FootprintW, fb.FootprintH, tl);
        CellCoord ex = new(tl.X + rel.X, tl.Y + rel.Y);
        if (InMap(ex) && _map.IsPassableTerrain(ex, cls, Setup.Rules.Terrain)) return true;
        for (int dy = 0; dy < fb.FootprintH; dy++)
            for (int dx = 0; dx < fb.FootprintW; dx++)
                if (InMap(new CellCoord(tl.X + dx, tl.Y + dy)) && _map.IsPassableTerrain(new CellCoord(tl.X + dx, tl.Y + dy), cls, Setup.Rules.Terrain)) return true;
        return false;
    }

    private bool SpawnProducedVehicle(byte player, int type, CellCoord factoryTopLeft, BuildingType factory, bool applyRally, CellCoord? rally)
    {
        VehicleType vt = RulesOf(player).Vehicles[type];
        CellCoord exitRel = OrientedOffset(factory.Exit, factory.FootprintW, factory.FootprintH, factoryTopLeft);   // M4-16
        CellCoord exit = new(factoryTopLeft.X + exitRel.X, factoryTopLeft.Y + exitRel.Y);
        if (!FindFreeExitCell(exit, MoveClass.Of(vt), out CellCoord cell)) return false;
        EntityId id = SpawnVehicle(player, type, cell);
        if (id.IsNone) return false;
        OnStatsBuilt(player, ActorKind.Vehicle);   // M4-06
        if (applyRally && rally is CellCoord rc && vt.HarvestCapacity <= 0 && rc != cell)
            AssignMission(new ActorRef(ActorKind.Vehicle, id), MissionKind.Move, rc);
        return true;
    }

    private bool SpawnProducedInfantry(byte player, int type, CellCoord factoryTopLeft, BuildingType factory, CellCoord? rally)
    {
        CellCoord exitRelInf = OrientedOffset(factory.Exit, factory.FootprintW, factory.FootprintH, factoryTopLeft);   // M4-16
        CellCoord exit = new(factoryTopLeft.X + exitRelInf.X, factoryTopLeft.Y + exitRelInf.Y);
        if (!FindFreeExitCell(exit, Locomotion.Foot, out CellCoord cell)) return false;
        EntityId id = SpawnInfantry(player, type, cell);
        if (id.IsNone) return false;
        OnStatsBuilt(player, ActorKind.Infantry);   // M4-06
        if (rally is CellCoord rc && rc != cell) AssignMission(new ActorRef(ActorKind.Infantry, id), MissionKind.Move, rc);
        return true;
    }

    /// <summary>
    /// M4-16: a footprint-relative cell (a factory exit or a refinery dock, authored for a building facing south, i.e. toward the
    /// map centre from the north half) as it applies to a building at <paramref name="topLeft"/>: unchanged when the footprint's centre
    /// lies in the north half of the map (or on the middle row), turned by 180 degrees inside the footprint when it lies in the south
    /// half. A base and its point mirror thus have mirrored exits and docks: before, the south-east base's units and harvesters left and
    /// entered on the side facing away from the enemy and the centre. Map-dependent geometry only; no state.
    /// </summary>
    internal CellCoord OrientedOffset(CellCoord rel, int footprintW, int footprintH, CellCoord topLeft)
    {
        if (topLeft.Y * 2 + footprintH <= Setup.Map.Height) return rel;
        return new CellCoord(footprintW - 1 - rel.X, footprintH - 1 - rel.Y);
    }

    /// <summary>
    /// The exit cell if free, else the nearest free passable cell: rings of growing Chebyshev radius around the exit. Free = in the map,
    /// passable for the locomotion, no building, no vehicle, no infantry. M4-16: within a ring the cell nearest the map centre wins
    /// (squared, in half cells), then the one nearest the line exit -> centre, left side first (as PathService.LineKey), then scan order;
    /// a plain top-row-first scan sent units of the bottom-right base out behind their factory (start-0 bias).
    /// </summary>
    private bool FindFreeExitCell(CellCoord exit, Locomotion loco, out CellCoord found)
    {
        int cx2 = Setup.Map.Width - 1, cy2 = Setup.Map.Height - 1;   // map centre in half cells
        for (int r = 0; r <= MaxExitSearchRing; r++)
        {
            bool any = false;
            long bestD = 0, bestK = 0;
            found = default;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(exit.X + dx, exit.Y + dy);
                    if (!InMap(c) || !_map.IsPassableTerrain(c, loco, Setup.Rules.Terrain)) continue;
                    if (!_map.BuildingAt(c).IsNone || !_map.VehicleAt(c).IsNone || ActorStandsOn(c)) continue;
                    long ox = c.X * 2L - cx2, oy = c.Y * 2L - cy2;
                    long d = ox * ox + oy * oy;
                    long ex = exit.X * 2L - cx2, ey = exit.Y * 2L - cy2;
                    long cross = (c.X * 2L - exit.X * 2L) * (-ey) - (c.Y * 2L - exit.Y * 2L) * (-ex);
                    long k = cross >= 0 ? cross * 2 : -cross * 2 + 1;
                    if (any && (d > bestD || (d == bestD && k >= bestK))) continue;
                    any = true; bestD = d; bestK = k; found = c;
                }
            if (any) return true;
        }
        found = default;
        return false;
    }

    private bool ActorStandsOn(CellCoord c)
    {
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i) && _units.Position[i].ToCell() == c) return true;
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && _infantry.Position[i].ToCell() == c) return true;
        return false;
    }

    /// <summary>
    /// Repair: while on and damaged, each tick heals RepairHpPerTick (less near full) and costs RepairCostPermille × Cost / MaxHealth / 1000
    /// per hp-point; the fraction is carried in ProductionState.RepairFrac so no credit is lost to rounding. Stops when full or when the
    /// owner cannot pay that tick.
    /// </summary>
    private void RunRepairs()
    {
        ProductionState s = _production;
        EconomyRules eco = Setup.Rules.Economy;
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (s.RepairGen[i] == 0) continue;
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Slots.GenerationOf(i) + 1 != s.RepairGen[i])
            { s.RepairGen[i] = 0; s.RepairFrac[i] = 0; continue; }
            BuildingType bt = BuildingTypeAt(i);
            byte owner = _buildings.Player[i];
            int hp = _buildings.Health[i];
            if (hp <= 0) continue;
            if (hp >= bt.MaxHealth || bt.MaxHealth <= 0) { s.RepairGen[i] = 0; s.RepairFrac[i] = 0; continue; }
            int heal = Math.Min(eco.RepairHpPerTick, bt.MaxHealth - hp);
            if (heal <= 0) continue;
            long den = (long)bt.MaxHealth * 1000;
            long num = s.RepairFrac[i] + (long)heal * eco.RepairCostPermille * bt.Cost;
            int credits = (int)(num / den);
            if (credits > Funds(owner)) { s.RepairGen[i] = 0; s.RepairFrac[i] = 0; continue; }   // broke: stop
            Spend(owner, credits);
            s.RepairFrac[i] = (int)(num % den);
            _buildings.Health[i] = hp + heal;
            if (hp + heal >= bt.MaxHealth) { s.RepairGen[i] = 0; s.RepairFrac[i] = 0; }
        }
    }

    // ---- win rule (D-062) ------------------------------------------------------------------------

    /// <summary>
    /// After cleanup: a non-neutral player who once owned a non-wall building and now owns none is Defeated (sticky). Players that never
    /// had a building (sandbox and test setups) are never defeated. Defeated players' units stay on the map (classic) but their commands
    /// are ignored and their queues stop.
    /// </summary>
    private void UpdateDefeat()
    {
        int n = _players.Count;
        Span<bool> has = n <= 1024 ? stackalloc bool[n] : new bool[n];
        RevertEmptyRuins();   // M4-15: emptied flipped neutral ruins go back to neutral first
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _garrison.HomeOwner[i] == GarrisonState.NoOwner   // M4-15: a flipped ruin is no base
                && !(BuildingTypeAt(i).Wall || BuildingTypeAt(i).Gate)) has[_buildings.Player[i]] = true;
        for (int v = 1; v < _units.Capacity; v++)   // M6-03: a packed rig is a base in transit, not a lost one
            if (_units.Slots.IsIndexAlive(v) && _units.Health[v] > 0
                && (_rigs.IsPackedRig(v) || RulesOf(_units.Player[v]).Vehicles[_units.TypeIndex[v]].DeploysInto >= 0))   // S19 M13-02 (D-173/D-205): a live founder (Command Ship, MCV) is a base too
                has[_units.Player[v]] = true;
        for (int p = 0; p < n; p++)
        {
            if (_players.IsNeutral((byte)p) || _players.Defeated[p]) continue;
            if (has[p]) _production.EverHadBase[p] = true;
            else if (_production.EverHadBase[p]) _players.Defeated[p] = true;
        }
    }

    private bool ComputeGameOver(out int winningTeam)
    {
        winningTeam = -1;
        bool anyDefeated = false, any = false;
        int team = -1;
        bool second = false;
        for (int p = 0; p < _players.Count; p++)
        {
            if (_players.IsNeutral((byte)p)) continue;
            if (_players.Defeated[p]) { anyDefeated = true; continue; }
            if (!any) { any = true; team = _players.Team[p]; }
            else if (_players.Team[p] != team) second = true;
        }
        if (!anyDefeated || second) return false;
        winningTeam = any ? team : -1;
        return true;
    }

    // ---- HUD -------------------------------------------------------------------------------------

    private IReadOnlyList<QueueView> BuildQueueViews(byte player)
    {
        var views = new List<QueueView>(ProductionState.Categories);
        for (int k = 0; k <= (int)ProductionCategory.Upgrade; k++)   // S15: the Upgrade lane is exposed too (it can be held)
        {
            var category = (ProductionCategory)k;
            int lane = ProductionState.Lane(player, category);
            int count = _production.QueueCount[lane];
            var items = new List<QueueItemView>(count);
            for (int j = 0; j < count; j++)
                items.Add(new QueueItemView(_production.QueueType[lane * _production.MaxQueue + j],
                    j == 0 ? _production.ProgressPermille(lane) : 0, j == 0 && _production.Stalled[lane], j == 0 && _production.Held[lane]));
            views.Add(new QueueView(category, items, _production.Ready[lane]));
        }
        return views;
    }

    private IReadOnlyList<(ProductionCategory Category, int TypeIndex)> BuildBuildableList(byte player)
    {
        var list = new List<(ProductionCategory, int)>();
        for (int k = 0; k <= (int)ProductionCategory.Defence; k++)
        {
            var category = (ProductionCategory)k;
            int count = category switch
            {
                ProductionCategory.Infantry => Setup.Rules.Infantry?.Count ?? 0,
                ProductionCategory.Vehicle or ProductionCategory.Aircraft => Setup.Rules.Vehicles.Count,   // M4-04
                ProductionCategory.Structure or ProductionCategory.Defence => BuildingTypeCount,
                _ => 0,
            };
            for (int t = 0; t < count; t++)
                if (CanBuild(player, category, t)) list.Add((category, t));
        }
        return list;
    }

    private IReadOnlyList<BuildOption> BuildOptionList(byte player)
    {
        var list = new List<BuildOption>();
        for (int k = 0; k <= (int)ProductionCategory.Defence; k++)
        {
            var category = (ProductionCategory)k;
            int count = category switch
            {
                ProductionCategory.Infantry => Setup.Rules.Infantry?.Count ?? 0,
                ProductionCategory.Vehicle or ProductionCategory.Aircraft => Setup.Rules.Vehicles.Count,   // M4-04
                ProductionCategory.Structure or ProductionCategory.Defence => BuildingTypeCount,
                _ => 0,
            };
            for (int t = 0; t < count; t++)
            {
                if (!IsUnlocked(player, category, t)) continue;
                TryTypeInfo(player, category, t, out int cost, out _, out _);
                list.Add(new BuildOption(category, t, true, QueueBlockReason(player, category, t), cost));
            }
        }
        return list;
    }
}
