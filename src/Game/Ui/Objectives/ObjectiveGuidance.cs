// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Ui.Fonts;
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Objectives;

/// <summary>
/// Objective guidance on the map (D-172, M11-05): the edge-of-screen arrow with distance, the pulsing ground marker, the minimap ping and
/// flash, the J / Tab / O keys, the one-line bar, the NEW OBJECTIVE banner and the short camera pan to a new objective. Reads the
/// <see cref="MissionHost"/> only; the sim never sees any of it.
/// </summary>
internal sealed class ObjectiveGuidance
{
    private static readonly Color Orange = new(255, 120, 30);
    private static readonly Color Gold = new(0xF0, 0xD0, 0x80);
    private static readonly Color Dark = new(24, 18, 10, 235);
    private const int Pad = 8;

    private readonly MissionHost _host;
    private readonly CameraPan _pan = new();
    private float _time;

    /// <summary>Creates the guidance over a mission host; <paramref name="briefing"/> is the briefing text shown with the O list (may be empty).</summary>
    public ObjectiveGuidance(MissionHost host, string briefing)
    {
        _host = host;
        BriefingLines = FormatBriefing(briefing);
    }

    /// <summary>The state and timers (exposed for tests and the overlay).</summary>
    public ObjectiveGuidanceModel Model { get; } = new();

    /// <summary>The briefing as short display lines (Markdown marks removed, wrapped, capped).</summary>
    public List<string> BriefingLines { get; }

    private List<MissionObjective> Visible() => _host.VisibleObjectives.ToList();

    private bool HasCell(MissionObjective o) => _host.TryObjectiveCell(o, out _);

    private MissionObjective? Guided() => Model.Guided(Visible(), HasCell);

    /// <summary>
    /// Per frame: reads J (jump to the objective), Tab (next objective with a target, and jump) and O (full list), watches for new
    /// objectives, and runs the camera pan. Returns the world point to centre the camera on this frame, or null to leave it alone.
    /// </summary>
    /// <param name="input">This frame's input (the screen calls this only when no text box has the keyboard).</param>
    /// <param name="dt">Seconds since the last frame.</param>
    /// <param name="tick">Sim tick (a game that is already running does not announce its first objectives).</param>
    /// <param name="cameraCentre">World point at the middle of the map view now.</param>
    /// <param name="cueEnabled">The "Camera cue on new objective" setting.</param>
    /// <param name="scriptCue">True when the script's own camera hint moved the camera this frame (it wins over the automatic pan).</param>
    public Vector2? Update(InputFrame input, float dt, int tick, Vector2 cameraCentre, bool cueEnabled, bool scriptCue)
    {
        _time += dt;
        List<MissionObjective> visible = Visible();
        Model.Observe(visible, dt, tick <= 100, _host.Label);
        Vector2? jump = null;
        if (input.ConsumePressed(Keys.O)) Model.ToggleExpanded();
        if (input.ConsumePressed(Keys.J) && Guided() is { } g && _host.TryObjectiveCell(g, out CellCoord jc)) jump = Camera.CentreOf(jc);
        if (input.ConsumePressed(Keys.Tab) && Model.Cycle(visible, HasCell) is { } next && _host.TryObjectiveCell(next, out CellCoord tc)) jump = Camera.CentreOf(tc);
        if (Model.TakeArrival() is { } id && cueEnabled && !scriptCue && visible.Find(o => o.Id == id) is { } arrived
            && _host.TryObjectiveCell(arrived, out CellCoord ac))
            _pan.Start(cameraCentre, Camera.CentreOf(ac));
        if (jump is not null || input.IsDown(Keys.Left) || input.IsDown(Keys.Right) || input.IsDown(Keys.Up) || input.IsDown(Keys.Down) || input.MiddleDragging || scriptCue)
            _pan.Cancel();
        return jump ?? _pan.Step(dt);
    }

    // ---- the one-line bar and the banner ----

    /// <summary>The lines of the collapsed bar: the objective line, the key hint, then any timer lines. Fitted to <paramref name="maxWidthPx"/>.</summary>
    public List<string> BarLines(List<string> timerLines, int scale, int maxWidthPx)
    {
        var lines = new List<string> { Fit(Model.OneLine(Visible(), _host.Label), scale, maxWidthPx), "J jump to objective   Tab next objective   O list" };
        lines.AddRange(timerLines);
        return lines;
    }

    /// <summary>The bar's screen rectangle: top centre of the map view.</summary>
    public static Rectangle BarRect(List<string> lines, int scale, int mapWidth)
    {
        int w = lines.Max(l => BitmapFont.Measure(l, scale)) + 2 * Pad;
        return new Rectangle(Math.Max(0, (mapWidth - w) / 2), 8, w, Hud.HeightOf(lines.Count, scale));
    }

    /// <summary>Draws the collapsed bar and returns its rectangle.</summary>
    public Rectangle DrawBar(SpriteBatch sb, Texture2D pixel, int scale, int mapWidth, List<string> timerLines)
    {
        List<string> lines = BarLines(timerLines, scale, Math.Max(100, mapWidth - 32));
        Rectangle r = BarRect(lines, scale, mapWidth);
        Hud.Panel(sb, pixel, r.Location, lines, scale);
        Shapes.Outline(sb, pixel, r, Orange, 1);
        return r;
    }

    /// <summary>Draws the NEW OBJECTIVE / COMPLETE banner for about three seconds, fading at the end, just below <paramref name="barBottom"/>.</summary>
    public void DrawBanner(SpriteBatch sb, Texture2D pixel, int scale, int mapWidth, int barBottom)
    {
        if (Model.Banner.Length == 0 || Model.BannerLeft <= 0f) return;
        byte a = (byte)(255 * Math.Clamp(Model.BannerLeft / 0.5f, 0f, 1f));
        int s = scale + 1;
        string text = Fit(Model.Banner, s, Math.Max(100, mapWidth - 40));
        int w = BitmapFont.Measure(text, s) + 2 * Pad * 2, h = Hud.HeightOf(1, s) + 8;
        var r = new Rectangle(Math.Max(0, (mapWidth - w) / 2), barBottom + 8, w, h);
        Shapes.Fill(sb, pixel, r, Shapes.Alpha(Dark, (byte)(Dark.A * a / 255)));
        Shapes.Outline(sb, pixel, r, Shapes.Alpha(Orange, a), 2);
        BitmapFont.Draw(sb, pixel, text, new Vector2(r.X + Pad * 2, r.Y + 6), Shapes.Alpha(Gold, a), s);
    }

    // ---- on the map ----

    /// <summary>
    /// Draws the pulsing ground diamond with a short label when the target is inside <paramref name="view"/>, or the edge arrow with a label
    /// and distance when it is not.
    /// </summary>
    public void DrawWorld(SpriteBatch sb, Texture2D pixel, Camera camera, Rectangle view, int scale)
    {
        if (Guided() is not { } o || !_host.TryObjectiveCell(o, out CellCoord cell)) return;
        Vector2 target = camera.ScreenFromWorld(Camera.CentreOf(cell));
        ArrowPlacement p = GuidanceMath.Place(target, view, 36);
        string label = GuidanceMath.Trim(_host.Label(o.Text), 30);
        float pulse = 0.5f + 0.5f * MathF.Sin(_time * 5f);
        if (p.OnScreen)
        {
            float r = 14f + 6f * pulse;
            Diamond(sb, pixel, target, r, Orange, 3);
            Diamond(sb, pixel, target, 5f, Gold, 2);
            int w = BitmapFont.Measure(label, scale);
            BitmapFont.Draw(sb, pixel, label, new Vector2(target.X - w / 2f, target.Y - r - BitmapFont.LineHeight(scale, FontWeight.Regular) - 4), Orange, scale);
            return;
        }

        Vector2 side = new(-p.Direction.Y, p.Direction.X);
        Vector2 tip = p.Edge, back = tip - p.Direction * 26f;
        for (int i = -14; i <= 14; i += 2) Shapes.Line(sb, pixel, tip, back + side * i, Orange, 3);
        Vector2 here = camera.WorldFromScreen(new Point(view.X + view.Width / 2, view.Y + view.Height / 2));
        string dist = GuidanceMath.DistanceText(Camera.CellAt(here), cell);
        var box = new List<string> { label.ToUpperInvariant(), dist, "J: jump there" };
        int bw = box.Max(l => BitmapFont.Measure(l, scale)) + 2 * Pad, bh = Hud.HeightOf(box.Count, scale);
        Vector2 c = back - p.Direction * (10f + MathF.Abs(p.Direction.X) * bw / 2f + MathF.Abs(p.Direction.Y) * bh / 2f);
        var rect = new Rectangle((int)(c.X - bw / 2f), (int)(c.Y - bh / 2f), bw, bh);
        rect.X = Math.Clamp(rect.X, view.Left + 4, Math.Max(view.Left + 4, view.Right - bw - 4));
        rect.Y = Math.Clamp(rect.Y, view.Top + 4, Math.Max(view.Top + 4, view.Bottom - bh - 4));
        Hud.Panel(sb, pixel, rect.Location, box, scale);
        Shapes.Outline(sb, pixel, rect, Orange, 2);
    }

    /// <summary>Draws the ping ring on the minimap at the target, and a bright frame while a new objective's flash runs.</summary>
    public void DrawMinimap(SpriteBatch sb, Texture2D pixel, Rectangle area, int mapWidth, int mapHeight)
    {
        if (Guided() is not { } o || !_host.TryObjectiveCell(o, out CellCoord cell)) return;
        MinimapMath m = MinimapMath.Fit(area, mapWidth, mapHeight);
        if (m.Image.IsEmpty) return;
        Vector2 at = m.PointFromCell(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
        float pulse = (_time * 1.5f) % 1f;
        Shapes.Circle(sb, pixel, at, 4f + 6f * pulse, Shapes.Alpha(Orange, (byte)(255 * (1f - pulse))), 2, 24);
        Shapes.Circle(sb, pixel, at, 4f, Orange, 2, 24);
        Shapes.Fill(sb, pixel, new Rectangle((int)at.X - 1, (int)at.Y - 1, 3, 3), Gold);
        if (Model.FlashLeft > 0f && ((int)(_time * 6f) & 1) == 0) Shapes.Outline(sb, pixel, area, Orange, 3);
    }

    private static void Diamond(SpriteBatch sb, Texture2D pixel, Vector2 c, float r, Color colour, int thickness)
    {
        Vector2 n = c + new Vector2(0, -r), e = c + new Vector2(r * 1.6f, 0), s = c + new Vector2(0, r), w = c + new Vector2(-r * 1.6f, 0);
        Shapes.Line(sb, pixel, n, e, colour, thickness);
        Shapes.Line(sb, pixel, e, s, colour, thickness);
        Shapes.Line(sb, pixel, s, w, colour, thickness);
        Shapes.Line(sb, pixel, w, n, colour, thickness);
    }

    /// <summary>The mission's briefing text for the O list: from the live session, else read from the mission folder; empty when unavailable.</summary>
    public static string BriefingOf(Rts.Game.Sandbox.MissionGame game)
    {
        if (game.Session is { } session) return session.Briefing;
        try
        {
            string data = Path.GetFullPath(Path.Combine(game.RulesDir, ".."));
            return File.ReadAllText(Path.Combine(MissionLoader.FolderOf(data, game.Def.Id), game.Def.BriefingFile));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return "";
        }
    }

    /// <summary>Cuts <paramref name="text"/> with "..." until it measures at most <paramref name="maxPx"/> pixels.</summary>
    public static string Fit(string text, int scale, int maxPx)
    {
        string t = text;
        while (t.Length > 4 && BitmapFont.Measure(t, scale) > maxPx) t = GuidanceMath.Trim(t, t.Length - 2);
        return t;
    }

    /// <summary>Briefing Markdown as plain wrapped lines: heading marks, emphasis marks and Archive link brackets removed; at most 14 lines.</summary>
    public static List<string> FormatBriefing(string briefing)
    {
        var lines = new List<string>();
        foreach (string raw in briefing.Replace("\r", "").Split('\n'))
        {
            string t = raw.Trim().TrimStart('#').Replace("**", "").Replace("*", "").Replace("[[", "").Replace("]]", "").Trim();
            if (t.Length == 0) continue;
            lines.AddRange(Rts.Game.Screens.MissionOverlay.Wrap(t, 70));
        }

        if (lines.Count > 14) { lines = lines.GetRange(0, 14); lines.Add("..."); }
        return lines;
    }
}
