// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Controls;

/// <summary>
/// M11-10e (audit F2, O7): which cursor to show over the map, decided by the same tree <see cref="OrderController.RightClick"/> walks
/// for every selected unit, so the cursor always matches the order a right-click would give. <see cref="OrderController"/> is not
/// touched: this file repeats its decision order (deploy cell, building click, attack, board, salvage, harvest, landing unload,
/// move) and uses the same pure helpers (<see cref="GarrisonOrders.Classify"/>, <see cref="M3Orders"/>, <see cref="SalvageOrders"/>,
/// <see cref="LandingOrders"/>). When the order tree changes, change <see cref="Decide"/> with it (a test pins every branch).
/// <see cref="Decide"/> is pure; <see cref="Probe"/> gathers its inputs from the live game the way <c>RightClick</c> does.
/// With several units selected the cursor shows the most specific action any of them would take: a special action (capture,
/// repair, garrison, enter, salvage, harvest, unload) beats attack, which beats move, which beats blocked.
/// </summary>
public sealed class CursorPreview
{
    /// <summary>One selected unit as the decision needs it.</summary>
    /// <param name="Kind">Vehicle or infantry.</param>
    /// <param name="TypeIndex">Index into the rules table of that kind.</param>
    /// <param name="Passengers">Passengers aboard (a vehicle only).</param>
    public readonly record struct Subject(ActorKind Kind, int TypeIndex, int Passengers);

    /// <summary>What is under the cursor, already looked up.</summary>
    public readonly record struct Target
    {
        /// <summary>The cell under the cursor.</summary>
        public CellCoord Cell { get; init; }

        /// <summary>True when <see cref="Cell"/> is on the map.</summary>
        public bool CellInMap { get; init; }

        /// <summary>True when an enemy actor (unit or building) is under the cursor.</summary>
        public bool HasEnemy { get; init; }

        /// <summary>The building under the cursor (enemy, or own when no enemy is there), or null.</summary>
        public BuildingView? Building { get; init; }

        /// <summary>The local player's relation to <see cref="Building"/>'s owner.</summary>
        public Relation BuildingRelation { get; init; }

        /// <summary>The type index of an own vehicle under the cursor that is not selected, or -1.</summary>
        public int OwnVehicleType { get; init; }

        /// <summary>True when a wreck lies on the cell.</summary>
        public bool WreckHere { get; init; }

        /// <summary>True when the cell is an ash (resource) field cell the player knows about.</summary>
        public bool FieldHere { get; init; }

        /// <summary>True when foot soldiers can stand on the cell (a landing craft can unload there).</summary>
        public bool FootStandable { get; init; }

        /// <summary>True when one of the player's own units or buildings is under the cursor.</summary>
        public bool OverOwn { get; init; }

        /// <summary>Terrain check for a locomotion on the cell (unknown cells must answer true so the fog leaks nothing).</summary>
        public Func<CellCoord, Locomotion, bool>? Passable { get; init; }
    }

    private readonly List<Subject> _subjects = new();
    private readonly List<WreckView> _wrecks = new();

    /// <summary>Priority used to merge the per-unit answers: higher wins.</summary>
    private static int Rank(CursorKind k) => k switch
    {
        CursorKind.Capture or CursorKind.Repair or CursorKind.Garrison or CursorKind.Enter or CursorKind.Salvage
            or CursorKind.Harvest or CursorKind.Unload or CursorKind.DeployAt => 3,
        CursorKind.Attack => 2,
        CursorKind.Move or CursorKind.AttackMove => 1,
        _ => 0,
    };

    /// <summary>
    /// HARVEST (documented stand-in): a vehicle with <c>harvest_capacity</c> above 0 right-clicking an ash field cell is sent to harvest
    /// there. The order side is added by HarvestOrders (session 15); at merge both should call one function.
    /// </summary>
    public static bool CanHarvestField(SimRules rules, ActorKind kind, int typeIndex) =>
        HarvestOrders.CanHarvest(rules, kind, typeIndex);

    /// <summary>The cursor for one selected unit, or <see cref="CursorKind.Select"/> when it would do nothing here.</summary>
    public static CursorKind For(SimRules rules, in Subject unit, in Target t, bool attackMoveArmed, bool deployArmed)
    {
        if (deployArmed)
        {
            return t.CellInMap && M3Orders.DeployModeOf(rules, unit.Kind, unit.TypeIndex) == DeployMode.AtCell ? CursorKind.DeployAt : CursorKind.Select;
        }

        BuildingClick click = BuildingClick.None;
        if (t.Building is BuildingView view) click = GarrisonOrders.Classify(rules, unit.Kind, unit.TypeIndex, view, t.BuildingRelation);

        switch (click)
        {
            case BuildingClick.Capture: return CursorKind.Capture;
            case BuildingClick.Repair: return CursorKind.Repair;
            case BuildingClick.Garrison: return CursorKind.Garrison;
            case BuildingClick.EnterTunnel: return CursorKind.Enter;
        }

        if (click == BuildingClick.Attack || (t.Building is null && t.HasEnemy)) return CursorKind.Attack;
        if (t.OwnVehicleType >= 0 && M3Orders.CanBoard(rules, unit.Kind, t.OwnVehicleType)) return CursorKind.Enter;
        if (!attackMoveArmed)
        {
            if (t.WreckHere && SalvageOrders.CanSalvage(rules, unit.Kind, unit.TypeIndex)) return CursorKind.Salvage;
            // M11-10a: the same classifier as the order side (OrderController -> HarvestOrders); an own refinery shows the enter cursor (Return).
            HarvestClick hc = HarvestOrders.Classify(rules, unit.Kind, unit.TypeIndex, t.FieldHere ? OverlayKind.Ashfall : OverlayKind.None,
                t.Building, t.BuildingRelation == Relation.Self);
            if (hc == HarvestClick.Harvest) return CursorKind.Harvest;
            if (hc == HarvestClick.Return) return CursorKind.Enter;
            if (unit.Kind == ActorKind.Vehicle && unit.Passengers > 0 && t.FootStandable
                && LandingOrders.IsLandingTransport(rules, unit.Kind, unit.TypeIndex)) return CursorKind.Unload;
        }

        if (!t.CellInMap) return CursorKind.Select;
        if (t.Passable is not null && !t.Passable(t.Cell, LocomotionOf(rules, unit))) return CursorKind.Blocked;
        return attackMoveArmed ? CursorKind.AttackMove : CursorKind.Move;
    }

    /// <summary>The cursor for a whole selection (see the class remarks for how the answers merge) and the hover rule.</summary>
    public static CursorKind Decide(SimRules rules, IReadOnlyList<Subject> selection, in Target t, bool attackMoveArmed, bool deployArmed)
    {
        if (selection.Count == 0) return t.OverOwn ? CursorKind.Hover : CursorKind.Select;
        CursorKind best = CursorKind.Select;
        int bestRank = -1;
        foreach (Subject s in selection)
        {
            CursorKind k = For(rules, s, t, attackMoveArmed, deployArmed);
            if (k == CursorKind.Select) continue;
            int r = Rank(k);
            if (r > bestRank)
            {
                best = k;
                bestRank = r;
            }
        }

        // Over an own unit or building a plain move is shown as the select hand: a left click selects it, like the original games.
        if (best == CursorKind.Move && t.OverOwn) return CursorKind.Hover;
        return best;
    }

    /// <summary>The locomotion a selected unit moves with (infantry walk; air units cross everything).</summary>
    private static Locomotion LocomotionOf(SimRules rules, in Subject unit)
    {
        if (unit.Kind == ActorKind.Vehicle && (uint)unit.TypeIndex < (uint)rules.Vehicles.Count) return rules.Vehicles[unit.TypeIndex].Locomotion;
        return Locomotion.Foot;
    }

    /// <summary>
    /// The cursor at a screen point in the live game: picks the target exactly as <c>OrderController.RightClick</c> does
    /// (<see cref="TargetPicker"/>, then the wreck list and the building under the cursor), then calls <see cref="Decide"/>.
    /// </summary>
    public CursorKind Probe(SimSnapshotSource source, Selection selection, Point screen, Camera camera, IReadOnlyList<RenderUnit> units,
        bool attackMoveArmed, bool deployArmed)
    {
        byte local = SimSnapshotSource.LocalPlayer;
        Vector2 world = camera.WorldFromScreen(screen);
        CellCoord cell = Camera.CellAt(world);
        bool inMap = source.Map.Contains(cell);
        SimRules rules = source.Rules;

        ActorRef? enemy = TargetPicker.Pick(units, source.Current, world, local);
        (ActorRef Target, int TypeIndex)? own = enemy is null
            ? TargetPicker.PickOwn(units, source.Current, world, local, selection.Contains)
            : null;
        EntityId buildingId = enemy is { Kind: ActorKind.Building } e ? e.Id : own is { Target.Kind: ActorKind.Building } o ? o.Target.Id : EntityId.None;
        BuildingView? building = null;
        if (!buildingId.IsNone)
        {
            foreach (BuildingView v in source.Current.Buildings)
                if (v.Id == buildingId) { building = v; break; }
        }

        // The own-unit hover also covers an own unit that is part of the selection (PickOwn skips those for boarding).
        bool overOwn = own is not null || OwnUnitAt(units, world, selection, local);
        PlayerView view = source.Sim.ViewFor(local);
        bool wreck = false;
        bool field = false;
        if (inMap)
        {
            view.CopyWrecks(_wrecks);
            wreck = SalvageOrders.WreckAt(_wrecks, cell);
            field = HarvestOrders.IsResource(view.OverlayAt(cell)) && view.VisionAt(cell) > 0;
        }

        _subjects.Clear();
        foreach (UnitKey key in selection.Units)
        {
            if (!source.TryGetTypeIndex(key, out int type)) continue;
            int passengers = key.Kind == ActorKind.Vehicle ? source.Sim.PassengersIn(key.Id) : 0;
            _subjects.Add(new Subject(key.Kind, type, passengers));
        }

        var target = new Target
        {
            Cell = cell,
            CellInMap = inMap,
            HasEnemy = enemy is not null,
            Building = building,
            BuildingRelation = building is BuildingView b ? source.Sim.RelationOf(local, b.Player) : Relation.Neutral,
            OwnVehicleType = own is { Target.Kind: ActorKind.Vehicle } mine ? mine.TypeIndex : -1,
            WreckHere = wreck,
            FieldHere = field,
            FootStandable = inMap && LandingOrders.FootStandable(rules, source.Map, cell),
            OverOwn = overOwn,
            Passable = (c, loco) => loco == Locomotion.Air || view.VisionAt(c) == 0 || view.IsPassable(c, loco),
        };
        return Decide(rules, _subjects, target, attackMoveArmed, deployArmed);
    }

    private static bool OwnUnitAt(IReadOnlyList<RenderUnit> units, Vector2 world, Selection selection, byte local)
    {
        foreach (RenderUnit u in units)
        {
            if (u.View.Player != local) continue;
            if (Vector2.DistanceSquared(u.World, world) <= Selection.PickRadiusPx * Selection.PickRadiusPx) return true;
        }

        return false;
    }
}
