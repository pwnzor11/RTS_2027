// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Rts.Sim.World;

namespace Rts.Sim;

// Player-view part of Simulation. OWNER: M1.5-06 (AI host). Additive only: no state, no sim behaviour.
public sealed partial class Simulation
{
    /// <summary>A read-only, fog-honest view of this simulation for one player (D-051). Allocation-free; cheap to
    /// call every tick. The view reads live state, so use it between Steps, never inside one.</summary>
    public PlayerView ViewFor(byte player)
    {
        if (player >= Setup.PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerView(this, player);
    }

    // ---- M3 view layers (M3-01). Fog rules, chosen and documented here:
    //  - Telegraphs: the owner and allies always; anyone else while the cell is in their team's vision now, or inside their
    //    Seismograph radius (IsSeismicRevealed).
    //  - Radiation and Blackout: only on cells the viewer's team sees NOW (no remembered layer: under fog they read 0).
    //  - Hardened: the viewer's own team's hardening everywhere; other teams' only on cells seen now.
    //  - Jammed: per viewer, true where an enemy jam radius covers the cell (minimap only), whatever the vision.
    //  - Omniscient (viewer -1): everything.

    /// <summary>True if <paramref name="viewer"/> (-1 = omniscient) may see telegraph <paramref name="t"/>.</summary>
    internal bool CanSeeTelegraph(int viewer, in Tunnels.Telegraph t)
    {
        if (viewer < 0) return true;
        var v = (byte)viewer;
        return IsFriendly(v, t.Owner) || IsVisibleTo(v, t.Cell) || IsSeismicRevealed(v, t.Cell);
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with the telegraphs <paramref name="viewer"/> (-1 = omniscient) may see,
    /// in list order.</summary>
    public void CopyTelegraphs(int viewer, List<TelegraphView> dest)
    {
        ArgumentNullException.ThrowIfNull(dest);
        dest.Clear();
        foreach (Tunnels.Telegraph t in _tunnels.Telegraphs)
            if (CanSeeTelegraph(viewer, t)) dest.Add(new TelegraphView(t.Cell, t.Owner, t.TicksLeft, t.Kind));
    }

    /// <summary>Radiation level of a cell as <paramref name="viewer"/> (-1 = omniscient) may know it (0 when not seen now).</summary>
    public int KnownRadiationAt(int viewer, CellCoord cell) =>
        _ground.RadiationAt(cell) is int level && level > 0 && (viewer < 0 || IsVisibleTo((byte)viewer, cell)) ? level : 0;

    /// <summary>True if <paramref name="viewer"/> (-1 = omniscient) knows of a Blackout cloud on the cell (seen now).</summary>
    public bool KnownBlackoutAt(int viewer, CellCoord cell) =>
        _ground.BlackoutAt(cell) > 0 && (viewer < 0 || IsVisibleTo((byte)viewer, cell));

    /// <summary>True if <paramref name="viewer"/> (-1 = omniscient) knows the cell is hardened: by its own team anywhere, by another
    /// team only where seen now.</summary>
    public bool KnownHardenedAt(int viewer, CellCoord cell)
    {
        if (!_ground.Contains(cell)) return false;
        ulong bits = _ground.Hardened[_ground.IndexOf(cell)];
        if (bits == 0) return false;
        if (viewer < 0) return true;
        var v = (byte)viewer;
        ReadOnlySpan<byte> team = TeamMembers(v);
        for (int k = 0; k < team.Length; k++)
            if (team[k] < Rts.Sim.Ground.GroundState.MaxHardeningPlayers && (bits & (1UL << team[k])) != 0) return true;
        return IsVisibleTo(v, cell);
    }

    /// <summary>True if the cell is jammed on <paramref name="viewer"/>'s minimap (never for the omniscient view).</summary>
    public bool IsJammedAt(int viewer, CellCoord cell) => viewer >= 0 && _ground.Contains(cell) && IsJammedFor((byte)viewer, cell);

    /// <summary>
    /// Fills <paramref name="dest"/> (length Width*Height, row-major) with <see cref="GroundLayers"/> bytes for
    /// <paramref name="viewer"/> (-1 = omniscient) under the fog rules above. Returns false (and leaves zeros) when every cell is 0.
    /// </summary>
    public bool FillGroundLayers(int viewer, byte[] dest)
    {
        ArgumentNullException.ThrowIfNull(dest);
        int w = _ground.Width, n = w * _ground.Height;
        if (dest.Length != n) throw new ArgumentException("dest must hold one byte per map cell.", nameof(dest));
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            var c = new CellCoord(i % w, i / w);
            int b = KnownRadiationAt(viewer, c) >> 4;
            if (KnownBlackoutAt(viewer, c)) b |= GroundLayers.Blackout;
            if (IsJammedAt(viewer, c)) b |= GroundLayers.Jammed;
            if (KnownHardenedAt(viewer, c)) b |= GroundLayers.Hardened;
            dest[i] = (byte)b;
            any |= b != 0;
        }
        return any;
    }

    /// <summary>M3-INT: a vision value (0/1/2) as the viewer's team may use it: a visible cell (2) inside a Blackout cloud drops to
    /// fog (1) unless <see cref="CanSeeThroughBlackout"/> applies. Matches <see cref="IsVisibleTo"/>.</summary>
    internal byte WithBlackout(byte viewer, CellCoord cell, byte value) =>
        value == 2 && _ground.BlackoutAt(cell) != 0 && !CanSeeThroughBlackout(viewer, cell) ? (byte)1 : value;

    /// <summary>M3-INT: apply <see cref="WithBlackout"/> to a whole row-major vision grid (only cells under a cloud are examined).</summary>
    internal void ApplyBlackoutToGrid(byte viewer, byte[] grid)
    {
        int[] b = _ground.Blackout;
        int w = Setup.Map.Width;
        for (int i = 0; i < b.Length && i < grid.Length; i++)
            if (b[i] != 0 && grid[i] == 2) grid[i] = WithBlackout(viewer, new CellCoord(i % w, i / w), 2);
    }

    /// <summary>True if the player could Place this structure at <paramref name="topLeft"/> now: the terrain/occupancy rules of
    /// <see cref="CanPlace"/> AND the D-062 build radius. Used by <see cref="PlayerView.CanPlace"/> so an AI never duplicates the rules.</summary>
    internal bool CanPlaceInRadius(byte player, int buildingType, CellCoord topLeft)
    {
        if (buildingType < 0 || buildingType >= BuildingTypeCount) return false;
        return CanPlace(player, buildingType, topLeft) == PlacementResult.Ok
            && WithinBuildRadius(player, Setup.Rules.Buildings[buildingType], topLeft);
    }

    // ---- M3-21: view data for the renderer (read-only; no sim state, no behaviour change) ----------------------------------------
    //  - Turret facing and the dive flag belong to units the snapshot already lists, so they follow its fog rule (enemies only while seen).
    //  - A gate has no stored open/closed state (D-076): it is "open" while a unit of its owner's team stands in its footprint. A unit the
    //    viewer cannot see (stealthed / burrowed enemy) does not count for a gate the viewer is shown.
    //  - Superweapons: only the viewer's own buildings (nobody is told that another player's superweapon charges); Viewer -1 lists all.

    /// <summary>Turret facing of a vehicle whose type has a turret (the hull facing until it first aims), else null.</summary>
    internal byte? TurretFacingOf(ActorKind kind, int i)
    {
        if (kind != ActorKind.Vehicle) return null;
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        if (!vt.HasTurret) return null;
        ref Combat.CombatEntry e = ref _combat.At(kind, i);
        return e.TurretInit ? e.TurretFacing : _units.Facing[i];
    }

    /// <summary>True while the actor is in the burrower's dive animation.</summary>
    internal bool IsDiving(ActorKind kind, int i) => _tunnels.LayerOf(kind, i) == ActorLayer.Diving;

    /// <summary>True if <paramref name="building"/> (a slot index) is a gate with a surface unit of the owner's team in its footprint,
    /// as <paramref name="viewer"/> (-1 = omniscient) may know it.</summary>
    internal bool IsGateOpenNow(int building, int viewer)
    {
        BuildingType bt = BuildingTypeAt(building);
        CellCoord tl = _buildings.Cell[building];
        byte owner = _buildings.Player[building];
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            MobileStore? store = StoreOf(kind);
            if (store == null) continue;
            for (int i = 1; i < store.Capacity; i++)
            {
                if (!store.Slots.IsIndexAlive(i) || !IsFriendly(owner, store.Player[i])) continue;
                CellCoord c = store.Position[i].ToCell();
                if (c.X < tl.X || c.Y < tl.Y || c.X >= tl.X + bt.FootprintW || c.Y >= tl.Y + bt.FootprintH) continue;
                if (IsUnderground(kind, i)) continue;
                if (viewer >= 0 && !IsFriendly((byte)viewer, store.Player[i]) && IsHiddenFrom((byte)viewer, kind, i)) continue;
                return true;
            }
        }
        return false;
    }

    /// <summary>The superweapon list for a snapshot: the viewer's own, or every player's (in slot order) for the omniscient view.</summary>
    internal IReadOnlyList<SuperweaponView> SuperweaponsFor(int viewer)
    {
        if (viewer >= 0) return SuperweaponViews((byte)viewer);
        List<SuperweaponView>? all = null;
        for (int p = 0; p < Setup.PlayerCount; p++)
        {
            IReadOnlyList<SuperweaponView> one = SuperweaponViews((byte)p);
            if (one.Count > 0) (all ??= new List<SuperweaponView>()).AddRange(one);
        }
        return all ?? (IReadOnlyList<SuperweaponView>)Array.Empty<SuperweaponView>();
    }
}
