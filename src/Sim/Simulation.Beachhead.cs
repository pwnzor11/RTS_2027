// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M7-07 (docs/design/11_m7_plan.md §2.5, 07_expedition.md §2.1): the beachhead zone and the water-building footprint rule.
// Zone of a player = every cell within BeachheadRadius (Chebyshev, nearest footprint cell to cell, the metric of the D-062 build radius)
// of a live own building whose type has BeachheadSource. The mask lives in the "beachhead" part and is rebuilt for ONE player when a
// source of that player appears, dies or changes hands (hooks in Simulation.M3.cs and CaptureBuilding), like hardening (D-076) but
// at once, so no dirty flag has to be saved. Only an Expedition player is gated; losing a source blocks new placement and destroys nothing.
public sealed partial class Simulation
{
    /// <summary>True if <paramref name="cell"/> (y * width + x) is inside <paramref name="player"/>'s beachhead zone.</summary>
    internal bool InBeachhead(byte player, int cell) =>
        player < _beachhead.Zone.Length && cell >= 0 && cell < Setup.Map.Width * Setup.Map.Height && _beachhead.Contains(player, cell);

    private bool IsExpedition(byte player) => player < _players.Count && _players.Faction[player] == Faction.Expedition;

    /// <summary>Hook: building <paramref name="i"/> was just placed.</summary>
    private void BeachheadOnPlaced(int i)
    {
        if (BuildingTypeAt(i).BeachheadSource) RecomputeBeachhead(_buildings.Player[i], -1);
    }

    /// <summary>Hook: building <paramref name="i"/> is about to be removed (still alive in its slot).</summary>
    private void BeachheadOnRemoving(int i)
    {
        if (BuildingTypeAt(i).BeachheadSource) RecomputeBeachhead(_buildings.Player[i], i);
    }

    /// <summary>Hook: building <paramref name="b"/> changed owner from <paramref name="oldOwner"/> (already set to the new one).</summary>
    private void BeachheadOnCaptured(int b, byte oldOwner)
    {
        if (!BuildingTypeAt(b).BeachheadSource) return;
        RecomputeBeachhead(oldOwner, b);
        RecomputeBeachhead(_buildings.Player[b], -1);
    }

    /// <summary>Rebuilds one player's mask from the live source buildings in slot order, leaving out slot <paramref name="exclude"/>.</summary>
    private void RecomputeBeachhead(byte player, int exclude)
    {
        if (player >= _beachhead.Zone.Length) return;
        _beachhead.Clear(player);
        int w = Setup.Map.Width, h = Setup.Map.Height;
        ulong[] zone = _beachhead.Zone[player];
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (i == exclude || !_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player) continue;
            BuildingType bt = BuildingTypeAt(i);
            if (!bt.BeachheadSource) continue;
            CellCoord tl = _buildings.Cell[i];
            int x0 = Math.Max(0, tl.X - bt.BeachheadRadius), x1 = Math.Min(w - 1, tl.X + bt.FootprintW - 1 + bt.BeachheadRadius);
            int y0 = Math.Max(0, tl.Y - bt.BeachheadRadius), y1 = Math.Min(h - 1, tl.Y + bt.FootprintH - 1 + bt.BeachheadRadius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int cell = y * w + x;
                    zone[cell >> 6] |= 1UL << (cell & 63);
                }
        }
    }

    /// <summary>True if the player owns a live building (any kind).</summary>
    private bool OwnsAnyBuilding(byte player)
    {
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player) return true;
        return false;
    }

    /// <summary>
    /// The M7-07 part of <see cref="CanPlace"/>, run after the footprint is known to lie on the map: terrain per cell (water buildings
    /// follow their <c>water</c> mask, every other structure refuses Water and Shallow) and, for an Expedition player, the beachhead
    /// zone. Returns <see cref="PlacementResult.Ok"/> if this part has no objection (the generic occupancy checks still follow).
    /// </summary>
    private PlacementResult CheckBeachheadAndWater(byte player, BuildingType bt, int x0, int y0)
    {
        int w = bt.FootprintW, h = bt.FootprintH;
        TerrainTable table = Setup.Rules.Terrain;
        bool mask = bt.WaterBuilding && bt.WaterMask.Count == w * h;
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new CellCoord(x0 + dx, y0 + dy);
                LandType land = _map.LandAt(c);
                bool isWater = land is LandType.Water or LandType.Shallow;
                bool wantWater = bt.WaterBuilding && (!mask || bt.WaterMask[dy * w + dx]);
                if (wantWater)
                {
                    // Water cell of a quay or shipyard: water terrain, free of overlay, building and vehicles (units: generic check).
                    if (!isWater || _map.OverlayAt(c) != OverlayKind.None || !_map.BuildingAt(c).IsNone || !_map.VehicleAt(c).IsNone)
                        return PlacementResult.Blocked;
                }
                else if (isWater || !_map.IsBuildable(c, table)) return PlacementResult.Blocked;
            }
        if (!IsExpedition(player) || player >= _beachhead.Zone.Length) return PlacementResult.Ok;
        // Bootstrap: a player with no building yet (the setup-phase depot) has no zone to be inside.
        if (!OwnsAnyBuilding(player)) return PlacementResult.Ok;
        int mw = Setup.Map.Width;
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                if (!_beachhead.Contains(player, (y0 + dy) * mw + x0 + dx)) return PlacementResult.OutsideBeachhead;
        return PlacementResult.Ok;
    }
}
