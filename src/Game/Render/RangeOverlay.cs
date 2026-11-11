// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// F10 in the sandbox, the Range rings key (L) in play (M13-15): the weapon range of each selected unit as a circle, and its minimum range as a red circle when it has one.
/// Ranges are in leptons; 256 leptons are one 64 px cell at zoom 1.
/// </summary>
public static class RangeOverlay
{
    private const int LinePx = 2;
    private const int Segments = 96;
    private static readonly Color RangeColour = new(0x9F, 0xE0, 0xFF);
    private static readonly Color MinRangeColour = new(0xFF, 0x60, 0x60);

    /// <summary>Draws the range circles of the selected units that have a weapon.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        if (frame.Sim is null) return;
        foreach (UnitKey key in frame.Selection.Units)
        {
            WeaponType? weapon = WeaponOf(frame.Sim, key);
            if (weapon is null || !frame.TryFind(key, out Vector2 world)) continue;

            Vector2 centre = camera.ScreenFromWorld(world);
            float range = LeptonsToScreen(weapon.Range, camera);
            Shapes.Circle(batch, pixel, centre, range, RangeColour, LinePx, Segments);
            if (weapon.MinRange > 0)
            {
                Shapes.Circle(batch, pixel, centre, LeptonsToScreen(weapon.MinRange, camera), MinRangeColour, LinePx, Segments);
            }
        }
    }

    /// <summary>
    /// M13-15: the weapon range and minimum range in leptons of a building type's primary weapon (a defence), or null when it has none.
    /// </summary>
    public static (int Range, int MinRange)? BuildingRange(SimRules rules, int typeIndex)
    {
        if ((uint)typeIndex >= (uint)rules.Buildings.Count) return null;
        int weapon = rules.Buildings[typeIndex].PrimaryWeapon;
        if (weapon < 0 || weapon >= rules.Weapons.Count || rules.Weapons[weapon].Range <= 0) return null;
        return (rules.Weapons[weapon].Range, rules.Weapons[weapon].MinRange);
    }

    /// <summary>M13-15: draws the range circles of one building (a selected defence), centred on its footprint.</summary>
    public static void DrawBuilding(SpriteBatch batch, Texture2D pixel, Camera camera, SimRules rules, BuildingView building)
    {
        if (BuildingRange(rules, building.TypeIndex) is not { } r) return;
        Vector2 centre = camera.ScreenFromWorld(new Vector2((building.Cell.X + building.FootprintW / 2f) * Camera.CellPx, (building.Cell.Y + building.FootprintH / 2f) * Camera.CellPx));
        Shapes.Circle(batch, pixel, centre, LeptonsToScreen(r.Range, camera), RangeColour, LinePx, Segments);
        if (r.MinRange > 0) Shapes.Circle(batch, pixel, centre, LeptonsToScreen(r.MinRange, camera), MinRangeColour, LinePx, Segments);
    }

    private static float LeptonsToScreen(int leptons, Camera camera) =>
        leptons * Camera.CellPx / (float)SimConstants.LeptonsPerCell * camera.Zoom;

    private static WeaponType? WeaponOf(Simulation sim, UnitKey key)
    {
        SimRules rules = sim.Setup.Rules;
        int weapon;
        if (key.Kind == ActorKind.Vehicle)
        {
            if (!sim.Units.Slots.IsAlive(key.Id)) return null;
            weapon = rules.Vehicles[sim.Units.TypeIndex[key.Id.Index]].PrimaryWeapon;
        }
        else
        {
            if (!sim.Infantry.Slots.IsAlive(key.Id)) return null;
            weapon = rules.Infantry[sim.Infantry.TypeIndex[key.Id.Index]].PrimaryWeapon;
        }
        return weapon >= 0 && weapon < rules.Weapons.Count ? rules.Weapons[weapon] : null;
    }
}
