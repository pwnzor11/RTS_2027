// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// The looping smoke and fire of a damaged building or vehicle (D-184), drawn from the existing effect sprites (<c>data/sprites/fx</c>). View only; every
/// frame is a function of the sim tick and an integer hash, so a replay screenshot shows the same flames.
/// <list type="bullet">
/// <item><b>Vehicles</b>: one point over the hull (<see cref="HullLift"/> above the ground point). Under half health a smoke plume, under a quarter two flames and a bigger plume.</item>
/// <item><b>Buildings</b>: the manifest's <c>damaged</c> anchors, as before, but flames only under a quarter (see <see cref="FxPlayer.DrawAnchors"/>); under a quarter this adds a
/// second, offset flame and plume per anchor. A building whose manifest has no damaged anchors gets a stand-in point on the middle of its footprint.</item>
/// </list>
/// </summary>
public static class DamageFx
{
    /// <summary>The smoke sprite id.</summary>
    public const string Smoke = "smoke_plume";

    /// <summary>The big flame sprite id.</summary>
    public const string FireLarge = "fire_large";

    /// <summary>The small flame sprite id.</summary>
    public const string FireSmall = "fire_small";

    /// <summary>How far above a vehicle's ground point the effects sit, as a share of its frame height (permille). 230 puts them on the engine deck of the 96 px tank.</summary>
    // ASSUMED: tuned on the Authority tank; other vehicles share the rule until they get their own anchor.
    public const int HullLiftPermille = 230;

    /// <summary>Pixels (zoom 1, before the unit scale) a vehicle's effect point is above its ground point.</summary>
    public static float HullLift(SpriteManifest manifest) => manifest.FrameH * HullLiftPermille / 1000f;

    /// <summary>Draws the effects of a damaged vehicle. <paramref name="origin"/> is its ground point on screen, <paramref name="scale"/> its sprite scale (unit scale times zoom).</summary>
    public static void DrawVehicle(SpriteBatch batch, SpriteLibrary library, SpriteManifest manifest, DamageTier tier, Vector2 origin, float scale, int tick, uint seed)
    {
        if (tier == DamageTier.None) return;
        Vector2 at = origin + new Vector2(0, -HullLift(manifest) * scale);
        int phase = (int)(seed % 12u);
        if (tier == DamageTier.Fire)
        {
            Draw(batch, library, FireSmall, at + new Vector2(-7, 3) * scale, scale * 0.95f, tick, phase);
            Draw(batch, library, FireSmall, at + new Vector2(8, 1) * scale, scale * 0.8f, tick, phase + 3);
        }

        Draw(batch, library, Smoke, at, scale * (tier == DamageTier.Fire ? 0.8f : 0.65f), tick, phase + 5);
    }

    /// <summary>
    /// Draws what a damaged building adds on top of its manifest anchors: under a quarter of health a second flame and plume next to every
    /// <c>damaged</c> anchor. <paramref name="origin"/> is the footprint's top-left on screen, <paramref name="zoom"/> the camera zoom.
    /// </summary>
    public static void DrawBuilding(SpriteBatch batch, SpriteLibrary library, SpriteManifest manifest, DamageTier tier, Vector2 origin, float zoom,
        int tick, int footprintW, int footprintH, uint seed)
    {
        if (tier == DamageTier.None) return;
        bool any = false;
        foreach (FxAnchor a in manifest.FxFor(SpriteStates.Damaged))
        {
            any = true;
            if (tier != DamageTier.Fire) continue;
            Vector2 at = origin + new Vector2(a.X - manifest.OriginX, a.Y - manifest.OriginY) * zoom;
            bool smoke = a.Kind.StartsWith("smoke", StringComparison.Ordinal);
            float side = (a.X + a.Y) % 2 == 0 ? 1 : -1;
            if (smoke) Draw(batch, library, Smoke, at + new Vector2(-8 * side, -6) * zoom, zoom * 1.2f, tick, a.Phase + 6);
            else Draw(batch, library, a.Kind, at + new Vector2(10 * side, 3) * zoom, zoom * 0.9f, tick, a.Phase + 4);
        }

        if (any) return;
        // no damaged anchors in this manifest (some walls and bunkers): a stand-in on the middle of the footprint, a little above the ground
        Vector2 mid = origin + new Vector2(footprintW * Camera.CellPx / 2f, footprintH * Camera.CellPx / 2f - 12) * zoom;
        int phase = (int)(seed % 12u);
        if (tier == DamageTier.Fire)
        {
            Draw(batch, library, FireLarge, mid, zoom, tick, phase);
            Draw(batch, library, FireSmall, mid + new Vector2(-14, 4) * zoom, zoom, tick, phase + 3);
        }

        Draw(batch, library, Smoke, mid, zoom * (tier == DamageTier.Fire ? 1.1f : 0.9f), tick, phase + 5);
    }

    private static void Draw(SpriteBatch batch, SpriteLibrary library, string kind, Vector2 at, float scale, int tick, int phase)
    {
        SpriteManifest m = library.Manifest(kind);
        library.Sheet(kind).Draw(batch, FxFrames.Anchored(m, tick, phase), at, Color.White, scale);
    }
}
