// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Vision;
using Rts.Sim.World;

namespace Rts.Sim.View;

/// <summary>
/// Read-only, fog-honest view of the simulation for one player (D-051). Everything an external command producer
/// (an AI) may know: the map, the player's own actors and cash, enemy and other actors only while they are inside
/// the player's current vision, and last-seen ghosts of enemy buildings. It exposes no sim array and has no setter.
/// A struct holding the simulation reference, so creating one is free. Queries that return lists fill a
/// caller-supplied list (cleared first), so a steady-state caller allocates nothing. The one exception is
/// <see cref="CopyKnownEnemyBuildings"/>, which uses the vision part's ghost listing (that API allocates).
/// Read between Steps only.
/// </summary>
public readonly struct PlayerView
{
    private readonly Simulation _sim;

    internal PlayerView(Simulation sim, byte player)
    {
        _sim = sim;
        Player = player;
    }

    /// <summary>The viewing player's slot.</summary>
    public byte Player { get; }

    /// <summary>Current simulation tick.</summary>
    public int Tick => _sim.Tick;

    /// <summary>Number of player slots in the game.</summary>
    public int PlayerCount => _sim.Setup.PlayerCount;

    /// <summary>Team id of the viewing player.</summary>
    public byte Team => _sim.Players.Team[Player];

    /// <summary>M5-01: the viewing player's latest commander directive (<see cref="Core.Directive.None"/> before the first one).
    /// The tiered AI reads it in Think (M5-02).</summary>
    public Directive Directive => _sim.DirectiveOf(Player);

    /// <summary>The viewing player's cash.</summary>
    public int Cash => _sim.Players.Cash[Player];

    /// <summary>HUD snapshot of the viewing player (cash, power, queues, ready structure, buildable list). Allocates; call once per decision.</summary>
    public PlayerHud Hud => _sim.HudFor(Player);

    /// <summary>True if the viewing player could place a ready structure of <paramref name="buildingType"/> with its
    /// top-left at <paramref name="topLeft"/> right now (terrain, occupancy, region sealing and the build radius).</summary>
    public bool CanPlace(int buildingType, CellCoord topLeft) => _sim.CanPlaceInRadius(Player, buildingType, topLeft);

    /// <summary>The rules tables (type data is public knowledge).</summary>
    public SimRules Rules => _sim.RulesOf(Player);

    /// <summary>How the viewing player regards <paramref name="other"/>.</summary>
    public Relation RelationOf(byte other) => _sim.RelationOf(Player, other);

    // ---- map ----------------------------------------------------------------------------------

    /// <summary>Map width in cells.</summary>
    public int MapWidth => _sim.Setup.Map.Width;

    /// <summary>Map height in cells.</summary>
    public int MapHeight => _sim.Setup.Map.Height;

    /// <summary>True if the cell is on the map.</summary>
    public bool InMap(CellCoord c) => _sim.Setup.Map.Contains(c);

    /// <summary>Land type of an on-map cell.</summary>
    public LandType LandAt(CellCoord c) => _sim.Map.LandAt(c);

    /// <summary>True if the terrain of the cell can be crossed with the given locomotion (terrain only: ignores units and buildings).</summary>
    public bool IsPassable(CellCoord c, Locomotion loco) =>
        InMap(c) && _sim.Map.IsPassableTerrain(c, loco, _sim.Setup.Rules.Terrain);

    /// <summary>M7-03: sea region of a cell for ships, 0 = not navigable. With <paramref name="deepOnly"/> the shallows count as land (large ships).
    /// Terrain only, so it reveals nothing the fog hides; equal non-zero labels mean "a ship can sail between them" (ignoring units and buildings).</summary>
    public int NavalRegionAt(CellCoord c, bool deepOnly = false) => _sim.NavalRegionAt(c, deepOnly);

    /// <summary>M7-03: true if the viewing player's own ship can reach <paramref name="target"/> by sea. False for a ship that is not
    /// the player's own, is not a ship, or is dead.</summary>
    public bool CanOwnShipReach(EntityId ship, CellCoord target) => _sim.OwnShipCanReach(Player, ship, target);

    /// <summary>M7-09: the viewing player's own carrier: aircraft docked in the hangar and the number of places. False for anything that is
    /// not an own live carrier (an enemy carrier's hangar is never shown).</summary>
    public bool OwnCarrierHangar(EntityId carrier, out int docked, out int slots) => _sim.OwnCarrierHangar(Player, carrier, out docked, out slots);

    /// <summary>0 = shroud, 1 = fog, 2 = visible now, for the viewing player.</summary>
    public byte VisionAt(CellCoord c) => _sim.WithBlackout(Player, c, _sim.Vision.VisionAt(Player, c));   // M3-INT: Blackout

    /// <summary>True if the cell is visible to the viewing player right now.</summary>
    public bool IsVisible(CellCoord c) => VisionAt(c) == 2;

    // ---- M3 layers (fog rules in Simulation.View.cs) ----------------------------------------------

    /// <summary>The viewing player's faction.</summary>
    public Faction Faction => _sim.Players.Faction[Player];

    /// <summary>True if the viewing player has researched upgrade <paramref name="upgradeIndex"/>.</summary>
    public bool HasUpgrade(int upgradeIndex) => _sim.HasUpgrade(Player, upgradeIndex);

    /// <summary>M4: the player's tech tier (1..3, <see cref="Simulation.TierOf"/>).</summary>
    public int Tier => _sim.TierOf(Player);

    /// <summary>M4: soldiers garrisoned in an own or allied building; -1 for any other building (fog-honest: an AI never learns
    /// an enemy garrison's size from this).</summary>
    public int OccupantsOf(EntityId building)
    {
        if (!_sim.Buildings.Slots.IsAlive(building)) return -1;
        Relation r = RelationOf(_sim.Buildings.Player[building.Index]);
        return r is Relation.Self or Relation.Ally ? _sim.OccupantsIn(building) : -1;
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with the telegraphs the player may see.</summary>
    public void CopyTelegraphs(List<TelegraphView> dest) => _sim.CopyTelegraphs(Player, dest);

    /// <summary>Radiation level as the player may know it (cells seen now; 0 elsewhere).</summary>
    public int RadiationAt(CellCoord c) => _sim.KnownRadiationAt(Player, c);

    /// <summary>True if the player knows of a Blackout cloud on the cell (seen now).</summary>
    public bool BlackoutAt(CellCoord c) => _sim.KnownBlackoutAt(Player, c);

    /// <summary>True if the player knows the cell is hardened (own team anywhere; others where seen now).</summary>
    public bool HardenedAt(CellCoord c) => _sim.KnownHardenedAt(Player, c);

    /// <summary>M7-07: True if the cell is inside the player's own beachhead zone (always false for a faction without one). Off-map: false.</summary>
    public bool InBeachhead(CellCoord c) => _sim.Setup.Map.Width > c.X && _sim.Setup.Map.Height > c.Y && c.X >= 0 && c.Y >= 0
        && _sim.InBeachhead(Player, c.Y * _sim.Setup.Map.Width + c.X);

    /// <summary>M7-07: the player's own beachhead mask, bit <c>cell % 64</c> of word <c>cell / 64</c> (cell = y * width + x), read-only.</summary>
    public ReadOnlySpan<ulong> BeachheadMask => Player < _sim.Beachhead.Zone.Length ? _sim.Beachhead.Zone[Player] : ReadOnlySpan<ulong>.Empty;

    /// <summary>True if the cell is jammed on the player's minimap (enemy Static Broadcast).</summary>
    public bool JammedAt(CellCoord c) => _sim.IsJammedAt(Player, c);

    // ---- actors -------------------------------------------------------------------------------

    /// <summary>Fills <paramref name="dest"/> (cleared first) with the player's own actors in slot order: vehicles,
    /// then infantry, then buildings (the latter only when <paramref name="includeBuildings"/>).</summary>
    public void CopyOwn(List<ViewActor> dest, bool includeBuildings = true)
    {
        dest.Clear();
        AddMobiles(dest, _sim.Units, ActorKind.Vehicle, own: true);
        AddMobiles(dest, _sim.Infantry, ActorKind.Infantry, own: true);
        if (includeBuildings) AddBuildings(dest, own: true, enemiesOnly: false);
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with every actor the player does not own that is inside
    /// current vision (Enemy, Ally and Neutral, tagged by <see cref="ViewActor.Relation"/>): vehicles, infantry, then
    /// buildings with at least one footprint cell in vision. Unseen actors are never listed.</summary>
    public void CopyVisible(List<ViewActor> dest)
    {
        dest.Clear();
        AddMobiles(dest, _sim.Units, ActorKind.Vehicle, own: false);
        AddMobiles(dest, _sim.Infantry, ActorKind.Infantry, own: false);
        AddBuildings(dest, own: false, enemiesOnly: false);
    }

    /// <summary>
    /// VET-5: veterancy of one of the viewing player's own vehicles or soldiers: its rank (0..3), its kill value and the kill value
    /// the next rank needs (0 at the top rank). (0, 0, 0) for any other actor, a stale reference, a building, a type that cannot
    /// rank, or when veterancy is off. Read-only; reads own actors only, so it never leaks an enemy's kill count.
    /// </summary>
    public (int Rank, int KillValue, int NextThreshold) VeterancyOf(ActorRef own)
    {
        if (own.Id.IsNone || own.Kind > ActorKind.Infantry) return (0, 0, 0);
        int i = own.Id.Index;
        if (!_sim.IsLive(own.Kind, i) || _sim.IdAt(own.Kind, i) != own.Id || _sim.PlayerAt(own.Kind, i) != Player) return (0, 0, 0);
        VeterancyRules vr = _sim.VetRules;
        if (!vr.Enabled) return (0, 0, 0);
        SimRules rules = Rules;
        bool canRank;
        int scale;
        if (own.Kind == ActorKind.Vehicle)
        {
            VehicleType t = rules.Vehicles[_sim.Units.TypeIndex[i]];
            (canRank, scale) = (t.CanRank, t.VetThresholdPermille);
        }
        else
        {
            InfantryType t = rules.Infantry[_sim.Infantry.TypeIndex[i]];
            (canRank, scale) = (t.CanRank, t.VetThresholdPermille);
        }

        if (!canRank) return (0, 0, 0);
        int rank = _sim.RankOf(own.Kind, i);
        int next = rank >= VeterancyRules.Ranks ? 0 : (int)Math.Max(1L, Math.Min(int.MaxValue, (long)vr.Thresholds[rank] * scale / 1000));
        return (rank, _sim.Veterancy.KillValue(own.Kind, i), next);
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with every enemy building the player knows of: those in
    /// vision now (live) plus last-seen ghosts whose footprint is currently out of vision (<see cref="ViewActor.Ghost"/>).</summary>
    public void CopyKnownEnemyBuildings(List<ViewActor> dest)
    {
        dest.Clear();
        AddBuildings(dest, own: false, enemiesOnly: true);
        SimRules rules = Rules;
        foreach ((int index, BuildingGhost g) in _sim.Vision.GhostsFor(Player))
        {
            if (FootprintVisible(g.Cell, g.FootprintW, g.FootprintH)) continue; // the live building is listed above
            if (RelationOf(g.Player) != Relation.Enemy) continue;
            int max = g.TypeIndex >= 0 && g.TypeIndex < rules.Buildings.Count ? rules.Buildings[g.TypeIndex].MaxHealth : g.Health;
            dest.Add(new ViewActor(new EntityId((ushort)index, 0), ActorKind.Building, g.Player, Relation.Enemy, g.TypeIndex,
                g.Cell, CentreOf(g.Cell, g.FootprintW, g.FootprintH), g.Health, max, MissionKind.None, false, EntityId.None,
                true, g.FootprintW, g.FootprintH));
        }
    }

    // ---- M7-06 home shipments (own state only) -------------------------------------------------

    /// <summary>M7-06: ticks until the viewing player's next shipment sails; -1 when none is scheduled (shipments off, or no dock).</summary>
    public int ShipmentEtaTicks
    {
        get
        {
            int next = _sim.Shipments.NextShipmentTick[Player];
            return next == 0 ? -1 : Math.Max(0, next - _sim.Tick);
        }
    }

    /// <summary>M7-06: decay steps the viewing player's shipments have taken (each shrinks the payload by <c>decay_permille</c>).</summary>
    public int ShipmentDecayStep => _sim.Shipments.DecayStep[Player];

    /// <summary>M7-06: credits the viewing player's next shipment would carry (0 when shipments are off).</summary>
    public int NextShipmentPayload => _sim.Setup.Rules.Shipments.Enabled ? _sim.ShipmentPayload(Player) : 0;

    /// <summary>M7-06: fills <paramref name="dest"/> (cleared first) with the viewing player's own tenders in flight, in creation order.</summary>
    public void CopyOwnTenders(List<TenderView> dest)
    {
        dest.Clear();
        foreach (Shipments.Tender t in _sim.Shipments.Tenders)
            if (t.Player == Player)
                dest.Add(new TenderView(t.Id, new EntityId((ushort)((uint)t.Vehicle >> 16), (ushort)(t.Vehicle & 0xFFFF)), new CellCoord(t.CellX, t.CellY), t.Payload));
    }

    /// <summary>M6: fills <paramref name="dest"/> (cleared first) with every wreck on the map in slot order. Wrecks are public (D-102).</summary>
    public void CopyWrecks(List<WreckView> dest)
    {
        dest.Clear();
        Salvage.SalvageState w = _sim.Salvage;
        for (int i = 1; i <= w.Capacity; i++)
            if (w.TicksLeft[i] > 0) dest.Add(new WreckView(i, new CellCoord(w.CellX[i], w.CellY[i]), w.Value[i], w.TicksLeft[i]));
    }

    /// <summary>M6: true if the vehicle is a packed rig (its type shows it anyway, so this is not hidden information).</summary>
    public bool IsPackedRig(EntityId vehicle) => _sim.Units.Slots.IsAlive(vehicle) && _sim.Rigs.IsPackedRig(vehicle.Index);

    /// <summary>M6: pack/unpack progress of an own actor in permille (0 = idle, not a rig, or not own).</summary>
    public int PackProgressPermille(ActorRef own) => _sim.PackProgressOf(Player, own);

    /// <summary>M6-09: index (in <see cref="Rules"/>.Buildings) of the building an own packed rig vehicle unpacks into, or -1 when the
    /// vehicle is not an own packed rig. One generic packed vehicle serves every rig, so this is the type it was packed from.</summary>
    public int UnpackTypeOf(EntityId vehicle) =>
        _sim.Units.Slots.IsAlive(vehicle) && _sim.Units.Player[vehicle.Index] == Player && _sim.Rigs.IsPackedRig(vehicle.Index)
            ? _sim.UnpackTypeOf(vehicle.Index) : -1;

    /// <summary>M6-09: true if the own packed rig <paramref name="vehicle"/> could start unpacking with the footprint's top-left at
    /// <paramref name="topLeft"/> now (the same site test the Pack command applies; no build radius). False for anything else.</summary>
    public bool CanUnpackAt(EntityId vehicle, CellCoord topLeft)
    {
        int into = UnpackTypeOf(vehicle);
        return into >= 0 && into < Rules.Buildings.Count && _sim.UnpackSiteOk(Player, into, topLeft, vehicle.Index);
    }

    /// <summary>M6-09: fills <paramref name="dest"/> (cleared first) with every live ash storm on the map in slot order. Storms are public
    /// (the cloud is on the ground for everyone to see; fog still hides it from the player's screen).</summary>
    public void CopyStorms(List<StormView> dest)
    {
        dest.Clear();
        Rts.Sim.Effects.EffectsState e = _sim.Effects;
        for (int s = 0; s < Rts.Sim.Effects.EffectsState.MaxStorms; s++)
            if (e.StormTicks[s] > 0) dest.Add(new StormView(s, new CellCoord(e.StormX[s], e.StormY[s]), e.StormRadius[s], e.StormTicks[s], e.StormOwner[s]));
    }

    /// <summary>Weapon range in leptons of an actor type, 0 when it has no weapon.</summary>
    public int WeaponRange(ActorKind kind, int typeIndex)
    {
        SimRules r = Rules;
        int weapon = kind switch
        {
            ActorKind.Vehicle when typeIndex >= 0 && typeIndex < r.Vehicles.Count => r.Vehicles[typeIndex].PrimaryWeapon,
            ActorKind.Infantry when typeIndex >= 0 && typeIndex < r.Infantry.Count => r.Infantry[typeIndex].PrimaryWeapon,
            ActorKind.Building when typeIndex >= 0 && typeIndex < r.Buildings.Count => r.Buildings[typeIndex].PrimaryWeapon,
            _ => -1,
        };
        return weapon >= 0 && weapon < r.Weapons.Count ? r.Weapons[weapon].Range : 0;
    }

    // ---- internals ----------------------------------------------------------------------------

    private void AddMobiles(List<ViewActor> dest, MobileStore s, ActorKind kind, bool own)
    {
        SimRules rules = Rules;
        for (int i = 1; i < s.Capacity; i++)
        {
            if (!s.Slots.IsIndexAlive(i)) continue;
            byte owner = s.Player[i];
            if ((owner == Player) != own) continue;
            CellCoord cell = s.Position[i].ToCell();
            if (!own && !IsVisible(cell)) continue;
            bool hidden = !own && _sim.IsHiddenFrom(Player, kind, i);   // M3: underground / stealthed enemy not revealed
            if (hidden) continue;
            bool underground = _sim.IsUnderground(kind, i), stealthed = _sim.IsStealthedNow(kind, i);
            bool friendly = own || RelationOf(owner) == Relation.Ally;
            int type = s.TypeIndex[i];
            int max = kind == ActorKind.Vehicle ? rules.Vehicles[type].MaxHealth : rules.Infantry[type].MaxHealth;
            if (own)
            {
                MissionKind m = s.Mission[i];
                bool idle = s.NextMission[i] == MissionKind.None
                    && (m == MissionKind.None || m == MissionKind.Sleep || m == MissionKind.Guard || m == MissionKind.Stop);
                dest.Add(new ViewActor(s.Slots.IdOf(i), kind, owner, Relation.Self, type, cell, s.Position[i], s.Health[i],
                    max, m, idle, s.Target[i], false, 1, 1) { Underground = underground, Stealthed = stealthed, Rank = _sim.RankOf(kind, i), Submerged = _sim.IsSubmergedShip(kind, i) });
            }
            else
            {
                dest.Add(new ViewActor(s.Slots.IdOf(i), kind, owner, RelationOf(owner), type, cell, s.Position[i], s.Health[i],
                    max, MissionKind.None, false, EntityId.None, false, 1, 1)
                {
                    Underground = underground, Stealthed = stealthed, Rank = _sim.RankOf(kind, i), Submerged = _sim.IsSubmergedShip(kind, i),
                    Revealed = !friendly && (underground || stealthed || _sim.IsSubmergedShip(kind, i)),   // listed although hidden-capable: revealed
                });
            }
        }
    }

    private void AddBuildings(List<ViewActor> dest, bool own, bool enemiesOnly)
    {
        SimRules rules = Rules;
        BuildingStore b = _sim.Buildings;
        for (int i = 1; i < b.Capacity; i++)
        {
            if (!b.Slots.IsIndexAlive(i)) continue;
            byte owner = b.Player[i];
            if ((owner == Player) != own) continue;
            BuildingType t = rules.Buildings[b.TypeIndex[i]];
            CellCoord cell = b.Cell[i];
            if (!own && !FootprintVisible(cell, t.FootprintW, t.FootprintH)) continue;
            Relation rel = own ? Relation.Self : RelationOf(owner);
            if (enemiesOnly && rel != Relation.Enemy) continue;
            LeptonCoord centre = CentreOf(cell, t.FootprintW, t.FootprintH);
            dest.Add(own
                ? new ViewActor(b.Slots.IdOf(i), ActorKind.Building, owner, rel, b.TypeIndex[i], cell, centre, b.Health[i],
                    t.MaxHealth, b.Mission[i], b.Mission[i] == MissionKind.None, b.Target[i], false, t.FootprintW, t.FootprintH)
                : new ViewActor(b.Slots.IdOf(i), ActorKind.Building, owner, rel, b.TypeIndex[i], cell, centre, b.Health[i],
                    t.MaxHealth, MissionKind.None, false, EntityId.None, false, t.FootprintW, t.FootprintH));
        }
    }

    private bool FootprintVisible(CellCoord topLeft, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (IsVisible(new CellCoord(topLeft.X + x, topLeft.Y + y))) return true;
        return false;
    }

    private static LeptonCoord CentreOf(CellCoord topLeft, int w, int h) =>
        new((topLeft.X << SimConstants.LeptonShift) + (w * SimConstants.LeptonsPerCell) / 2,
            (topLeft.Y << SimConstants.LeptonShift) + (h * SimConstants.LeptonsPerCell) / 2);
}
