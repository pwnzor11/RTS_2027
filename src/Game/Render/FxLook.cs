// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// The renderer-side look improvements of D-184, all view only (no sim change, no hash change; integer hashes and sim ticks, never the wall
/// clock): damage tiers with entry bursts, remains of the dead, dust behind moving vehicles, and the per-theater mood tint. <see cref="SceneRenderer"/>
/// owns one and calls <see cref="Observe"/> once per frame, <see cref="DrawGround"/> under the units, <see cref="DrawOverSprites"/> above them and
/// <see cref="DrawMood"/> over the finished world.
/// </summary>
public sealed class FxLook : IDisposable
{
    private readonly DamageTierTracker _tiers = new();
    private readonly DeathWatcher _deaths = new();
    private readonly DustEmitter _dust = new();
    private readonly RemainsBook _remains = new();
    private readonly PuffField _puffs = new();
    private readonly List<TierChange> _changes = new();
    private readonly List<DeathRecord> _dead = new();
    private readonly Dictionary<(RemainKind Kind, int A, int B, int Faction, int Variant), Texture2D> _textures = new();
    private Texture2D? _puffTexture;
    private Texture2D? _smokeTexture;

    /// <summary>
    /// Switches every D-184 look improvement off at once (damage tiers and their flames rule, remains, dust, mood tint), restoring today's look, to compare.
    /// Starts true when the <c>RTS_FXLOOK</c> environment variable is <c>off</c>. The infantry size is its own setting.
    /// </summary>
    public static bool Off { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("RTS_FXLOOK"), "off", StringComparison.OrdinalIgnoreCase);

    private bool _enabled = true;

    /// <summary>Remains, dust and damage effects on or off for this instance (the mood tint is separate); always off while <see cref="Off"/> is set.</summary>
    public bool Enabled { get => _enabled && !Off; set => _enabled = value; }

    /// <summary>The remains that exist now (for tests and the contact sheet).</summary>
    public RemainsBook Remains => _remains;

    /// <summary>The puffs alive now (for tests and the contact sheet).</summary>
    public PuffField Puffs => _puffs;

    /// <summary>
    /// Reads the snapshot once per sim tick: starts a burst for every building or vehicle that entered a higher damage tier, adds a remain for
    /// everything that died, adds dust behind moving vehicles, and drops what has expired.
    /// </summary>
    /// <param name="snapshot">The snapshot about to be drawn.</param>
    /// <param name="rules">Rules, or null (then only the expiry runs).</param>
    /// <param name="fx">The one-shot effect player the bursts go to.</param>
    /// <param name="mood">The theater's mood (its dust colour).</param>
    /// <param name="sprites">Sprite resolver, to find where a building's smoke comes out (its <c>damaged</c> anchors); null = the middle of the footprint.</param>
    public void Observe(SimSnapshot snapshot, SimRules? rules, FxPlayer fx, MoodGrade mood, SpriteResolver? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        int tick = snapshot.Tick;
        _remains.Prune(tick);
        _puffs.Prune(tick);
        if (!Enabled || rules is null)
        {
            return;
        }

        _tiers.Observe(snapshot, rules, _changes);
        foreach (TierChange c in _changes) Burst(snapshot, rules, fx, c, sprites);
        _deaths.Observe(snapshot, _dead);
        foreach (DeathRecord d in _dead) Bury(rules, d);
        _dust.Observe(snapshot, rules, _puffs, mood.DustColour);
    }

    // ---- bursts ------------------------------------------------------------------------------------------------------------------

    private void Burst(SimSnapshot snapshot, SimRules rules, FxPlayer fx, TierChange c, SpriteResolver? sprites)
    {
        int tick = snapshot.Tick;
        if (c.Building)
        {
            BuildingView b = snapshot.Buildings[c.Index];
            float cx = (b.Cell.X + b.FootprintW / 2f) * Camera.CellPx, cy = (b.Cell.Y + b.FootprintH / 2f) * Camera.CellPx - 14;
            if (sprites is not null)   // burst where the smoke comes out: the mean of the damaged anchors
            {
                SpriteManifest m = sprites.ForBuilding(rules, b).Manifest;
                int n = 0, sx = 0, sy = 0;
                foreach (FxAnchor a in m.FxFor(SpriteStates.Damaged)) { n++; sx += a.X - m.OriginX; sy += a.Y - m.OriginY; }
                if (n > 0) { cx = b.Cell.X * Camera.CellPx + sx / (float)n; cy = b.Cell.Y * Camera.CellPx + sy / (float)n; }
            }

            float spread = 10 * Math.Max(b.FootprintW, b.FootprintH);
            for (int i = 0; i < (c.To == DamageTier.Fire ? 4 : 3); i++)
            {
                uint h = DecorHash.Mix(0x4255_5253UL, b.Id.Index, tick, (uint)i);
                float dx = ((int)(h & 31) - 15.5f) / 15.5f * spread, dy = ((int)((h >> 5) & 15) - 7.5f) / 7.5f * spread * 0.5f;
                AddSmokePuff(cx + dx, cy + dy, tick, 0.9f + 0.1f * Math.Max(b.FootprintW, b.FootprintH));
            }

            fx.Spawn("sparks", new Vector2(cx, cy), tick, 2f);
            if (c.To == DamageTier.Fire) fx.Spawn("explosion_large", new Vector2(cx, cy), tick, Math.Min(0.9f, 0.35f + 0.12f * Math.Max(b.FootprintW, b.FootprintH)));
            return;
        }

        UnitView u = snapshot.Units[c.Index];
        if (u.Kind != UnitKind.Vehicle || (uint)u.TypeIndex >= (uint)rules.Vehicles.Count) return;
        Vector2 at = Camera.WorldFromLepton(u.Position) + new Vector2(0, -22 * SpriteLayout.UnitDrawScale);
        for (int i = 0; i < 3; i++)
        {
            uint h = DecorHash.Mix(0x5655_5253UL, u.Id.Index, tick, (uint)i);
            AddSmokePuff(at.X + ((int)(h & 15) - 7.5f), at.Y + ((int)((h >> 4) & 7) - 3.5f), tick, 0.7f);
        }

        fx.Spawn("sparks", at, tick, 1.5f);
        if (c.To == DamageTier.Fire) fx.Spawn("explosion_small", at, tick, 0.8f);
    }

    private void AddSmokePuff(float x, float y, int tick, float scale) =>
        _puffs.Add(new Puff(x, y, tick, 28, 0x3C3A38, 170, scale, 22, Smoke: true));

    // ---- remains -----------------------------------------------------------------------------------------------------------------

    private void Bury(SimRules rules, DeathRecord d)
    {
        Vector2 w = Camera.WorldFromLepton(d.Pos);
        int x = (int)MathF.Round(w.X), y = (int)MathF.Round(w.Y);
        uint seed = DecorHash.Mix(0x4445_4144UL, x, y, (uint)d.Tick);
        switch (d.Kind)
        {
            case RemainKind.BuildingRubble:
            {
                if ((uint)d.TypeIndex >= (uint)rules.Buildings.Count) return;
                BuildingType bt = rules.Buildings[d.TypeIndex];
                if (bt.Wall) return;   // walls die by the dozen and are one cell: no heap
                int side = Math.Max(d.Width, d.Height);
                _remains.Add(new Remain(RemainKind.Scorch, x, y, side * Camera.CellPx * 540 / 1000, 900, (int)bt.Faction, seed, d.Tick));
                _remains.Add(new Remain(RemainKind.BuildingRubble, x, y, d.Width, d.Height, (int)bt.Faction, seed, d.Tick));
                break;
            }
            case RemainKind.VehicleWreck:
            {
                if ((uint)d.TypeIndex >= (uint)rules.Vehicles.Count) return;
                VehicleType vt = rules.Vehicles[d.TypeIndex];
                if (vt.Locomotion is Locomotion.Air or Locomotion.Naval or Locomotion.Burrow) return;   // no wreck for aircraft, ships, burrowers
                _remains.Add(new Remain(RemainKind.Scorch, x, y + 4, 36, RemainsArt.GroundSquashPermille, (int)vt.Faction, seed, d.Tick));
                _remains.Add(new Remain(RemainKind.VehicleWreck, x, y, 0, 0, (int)vt.Faction, seed, d.Tick));
                break;
            }
            default:
                _remains.Add(new Remain(RemainKind.InfantryMark, x, y, 0, 0, 0, seed, d.Tick));
                break;
        }
    }

    // ---- drawing -----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Draws the remains and the puffs, on the ground: after the terrain and the ground layers, before the buildings and units. A vehicle wreck is not drawn
    /// where the sim itself has a wreck (the Left Behind salvage wrecks, <paramref name="simWrecks"/>), so nothing is drawn twice; its scorch is.
    /// </summary>
    public void DrawGround(SpriteBatch batch, Texture2D pixel, Camera camera, Viewport viewport, int tick, IReadOnlyList<WreckDraw>? simWrecks)
    {
        if (!Enabled) return;
        GraphicsDevice device = pixel.GraphicsDevice;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Remain r in _remains.Items)
            {
                if ((r.Kind == RemainKind.Scorch) != (pass == 0)) continue;
                int alpha = RemainsTiming.Alpha(r.Kind, tick - r.Born);
                if (alpha <= 0) continue;
                if (r.Kind == RemainKind.VehicleWreck && HasSimWreck(simWrecks, r)) continue;
                Rectangle dest = DestOf(r, camera, out int a, out int b);
                if (!dest.Intersects(viewport.Bounds)) continue;
                Texture2D tex = TextureOf(device, r, a, b);
                batch.Draw(tex, dest, Color.White * (alpha / 255f));
            }
        }

        DrawPuffs(batch, device, camera, viewport, tick);
    }

    /// <summary>Whether the sim has its own wreck (a Left Behind salvage wreck) on the cell a remain lies in.</summary>
    public static bool HasSimWreck(IReadOnlyList<WreckDraw>? wrecks, Remain r)
    {
        if (wrecks is null) return false;
        int cx = r.X < 0 ? -1 : r.X / Camera.CellPx, cy = r.Y < 0 ? -1 : r.Y / Camera.CellPx;
        foreach (WreckDraw w in wrecks) if (w.Cell.X == cx && w.Cell.Y == cy) return true;
        return false;
    }

    // screen rectangle of a remain, and the two numbers that key its texture (rubble: footprint cells; scorch: radius and squash; others 0)
    private static Rectangle DestOf(Remain r, Camera camera, out int a, out int b)
    {
        a = 0;
        b = 0;
        switch (r.Kind)
        {
            case RemainKind.BuildingRubble:
            {
                a = r.WidthCells;
                b = r.HeightCells;
                float left = r.X - r.WidthCells * Camera.CellPx / 2f - RemainsArt.RubbleMargin, top = r.Y - r.HeightCells * Camera.CellPx / 2f - RemainsArt.RubbleMargin;
                (int w, int h) = RemainsArt.RubbleSize(r.WidthCells, r.HeightCells);
                return camera.ScreenRect(left, top, left + w, top + h);
            }
            case RemainKind.VehicleWreck:
            {
                float u = SpriteLayout.UnitDrawScale;
                float left = r.X - RemainsArt.WreckOriginX * u, top = r.Y - RemainsArt.WreckOriginY * u;
                return camera.ScreenRect(left, top, left + RemainsArt.WreckW * u, top + RemainsArt.WreckH * u);
            }
            case RemainKind.InfantryMark:
            {
                float u = SpriteLayout.UnitDrawScale;
                return camera.ScreenRect(r.X - RemainsArt.MarkW * u / 2, r.Y - RemainsArt.MarkH * u / 2, r.X + RemainsArt.MarkW * u / 2, r.Y + RemainsArt.MarkH * u / 2);
            }
            default:
            {
                a = r.WidthCells;
                b = r.HeightCells;
                (int w, int h) = RemainsArt.ScorchSize(r.WidthCells, r.HeightCells);
                return camera.ScreenRect(r.X - w / 2f, r.Y - h / 2f, r.X + w / 2f, r.Y + h / 2f);
            }
        }
    }

    private Texture2D TextureOf(GraphicsDevice device, Remain r, int a, int b)
    {
        int variant = (int)(r.Seed % 4u);
        int faction = r.Kind is RemainKind.InfantryMark or RemainKind.Scorch ? 0 : r.Faction;
        (RemainKind, int, int, int, int) key = (r.Kind, a, b, faction, variant);
        if (_textures.TryGetValue(key, out Texture2D? cached)) return cached;
        uint seed = (uint)variant * 7919u + 13u;
        PropCanvas canvas = r.Kind switch
        {
            RemainKind.BuildingRubble => RemainsArt.Rubble(a, b, faction, seed),
            RemainKind.VehicleWreck => RemainsArt.Wreck(faction, seed),
            RemainKind.InfantryMark => RemainsArt.Mark(seed),
            _ => RemainsArt.Scorch(a, b, seed),
        };
        var tex = new Texture2D(device, canvas.Width, canvas.Height);
        tex.SetData(canvas.Rgba);
        _textures[key] = tex;
        return tex;
    }

    private void DrawPuffs(SpriteBatch batch, GraphicsDevice device, Camera camera, Viewport viewport, int tick)
    {
        if (_puffs.Items.Count == 0) return;
        if (_puffTexture is null)
        {
            _puffTexture = new Texture2D(device, PuffArt.Frames * PuffArt.FramePx, PuffArt.FramePx);
            _puffTexture.SetData(PuffArt.Render(false));
            _smokeTexture = new Texture2D(device, PuffArt.Frames * PuffArt.FramePx, PuffArt.FramePx);
            _smokeTexture.SetData(PuffArt.Render(true));
        }

        foreach (Puff p in _puffs.Items)
        {
            int age = tick - p.Born;
            int alpha = PuffField.Alpha(age, p.Life, p.MaxAlpha);
            if (alpha <= 0) continue;
            float grow = 1f + 0.8f * age / p.Life, size = PuffArt.FramePx * p.Scale * grow;
            float x = p.X, y = p.Y - p.Rise * age / p.Life;
            Rectangle dest = camera.ScreenRect(x - size / 2, y - size / 2, x + size / 2, y + size / 2);
            if (!dest.Intersects(viewport.Bounds)) continue;
            int frame = PuffField.Frame(age, p.Life);
            var tint = new Color((int)(p.Rgb >> 16 & 0xFF), (int)(p.Rgb >> 8 & 0xFF), (int)(p.Rgb & 0xFF));
            batch.Draw(p.Smoke ? _smokeTexture! : _puffTexture, dest, new Rectangle(frame * PuffArt.FramePx, 0, PuffArt.FramePx, PuffArt.FramePx), Shapes.Alpha(tint, (byte)alpha));
        }
    }

    /// <summary>
    /// Draws the looping smoke and fire of damaged vehicles and the extra flames of badly damaged buildings, above the sprites (smoke rises over what
    /// stands in front of it). Buildings still being built are skipped.
    /// </summary>
    public void DrawOverSprites(SpriteBatch batch, SpriteLibrary? library, SpriteResolver sprites, SceneFrame frame, RenderMemory memory, Camera camera, Viewport viewport)
    {
        if (!Enabled || library is null || frame.Rules is null) return;
        SimRules rules = frame.Rules;
        int tick = frame.Snapshot.Tick;
        Rectangle view = viewport.Bounds;
        view.Inflate(160, 160);
        foreach (BuildingView b in frame.Snapshot.Buildings)
        {
            if (b.Ghost || (uint)b.TypeIndex >= (uint)rules.Buildings.Count) continue;
            BuildingType bt = rules.Buildings[b.TypeIndex];
            DamageTier tier = DamageTiers.For(b.Health, bt.MaxHealth);
            if (tier == DamageTier.None || (bt.Wall && tier != DamageTier.Fire) || memory.BuildingAge(b.Id, tick) < SpriteStates.BuildAnimationTicks) continue;
            Vector2 origin = SpriteLayout.BuildingOrigin(camera, b.Cell);
            if (!view.Contains((int)origin.X + b.FootprintW * Camera.CellPx / 2, (int)origin.Y + b.FootprintH * Camera.CellPx / 2)) continue;
            if (bt.Wall) continue;
            DamageFx.DrawBuilding(batch, library, sprites.ForBuilding(rules, b).Manifest, tier, origin, camera.Zoom, tick, b.FootprintW, b.FootprintH, DecorHash.Mix(0x4246_5853UL, b.Id.Index, b.Cell.X, (uint)b.Cell.Y));
        }

        foreach (RenderUnit unit in frame.Units)
        {
            UnitView v = unit.View;
            if (v.Kind != UnitKind.Vehicle || v.Underground || (uint)v.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            VehicleType vt = rules.Vehicles[v.TypeIndex];
            if (vt.Locomotion == Locomotion.Air) continue;   // aircraft: no anchor over a flying hull yet
            DamageTier tier = DamageTiers.For(v.Health, vt.MaxHealth);
            if (tier == DamageTier.None) continue;
            Vector2 origin = SpriteLayout.UnitOrigin(camera, unit.World);
            if (!view.Contains((int)origin.X, (int)origin.Y)) continue;
            float scale = SpriteLayout.UnitSpriteScale(UnitRenderer.DrawScale(rules, v), camera.Zoom);
            DamageFx.DrawVehicle(batch, library, sprites.ForUnit(rules, v).Manifest, tier, origin, scale, tick, DecorHash.Mix(0x5646_5853UL, v.Id.Index, 0, 1));
        }
    }

    /// <summary>
    /// Lays the theater's mood tint over the world (<paramref name="area"/>, normally the map view): a flat colour blended in at the mood's
    /// strength, so haze, cold, brightness or warmth read as a grade. Does nothing for a theater with no <c>[mood]</c> or with RTS_MOOD=off.
    /// </summary>
    public static void DrawMood(SpriteBatch batch, Texture2D pixel, MoodGrade mood, Rectangle area)
    {
        if (!mood.Active) return;
        var tint = new Color((int)(mood.Tint >> 16 & 0xFF), (int)(mood.Tint >> 8 & 0xFF), (int)(mood.Tint & 0xFF));
        Shapes.Fill(batch, pixel, area, Shapes.Alpha(tint, (byte)mood.Strength));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Texture2D t in _textures.Values) t.Dispose();
        _textures.Clear();
        _puffTexture?.Dispose();
        _puffTexture = null;
        _smokeTexture?.Dispose();
        _smokeTexture = null;
    }
}
