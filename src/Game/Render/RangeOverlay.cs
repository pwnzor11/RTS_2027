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
/// F10: the weapon range of each selected unit as a circle, and its minimum range as a red circle when it has one.
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
