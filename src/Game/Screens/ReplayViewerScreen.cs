// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Saves;
using Rts.Game.Sprites;
using Rts.Game.Stats;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// Plays a recorded replay through the normal scene renderer. Read-only: no order, selection or sidebar reaches the simulation; only the
/// camera and the playback controls respond. Space pauses, 1 2 3 4 pick 1x 2x 4x 8x, '.' steps one tick, F or Page Down jumps ahead 10
/// seconds, F2 changes the fog view, R starts over, arrows or middle-drag pan, wheel zooms, Esc goes back. A banner says when the end is reached.
/// </summary>
public sealed class ReplayViewerScreen : IScreen
{
    private const float PanScreenPixelsPerSecond = 320f;

    private readonly GameServices _s;
    private readonly string _path;
    private ReplayPlayback _play;
    private readonly Camera _camera = new();
    private readonly OverlayToggles _overlays = new();
    private readonly Selection _selection = new();
    private readonly List<RenderUnit> _renderUnits = new();
    private bool _centred;
    private readonly StatsSampler _sampler = new();   // M10-17: graphs of the replay played so far

    /// <summary>Creates the viewer over an opened replay of the file <paramref name="path"/> (kept for Restart).</summary>
    public ReplayViewerScreen(GameServices services, string path, ReplayPlayback playback)
    {
        _s = services;
        _path = path;
        _play = playback;
    }

    /// <summary>
    /// Opens <paramref name="replay"/> and pushes the viewer. A replay that cannot be played (damaged, its map or mission is not in the
    /// data, the game data changed) gives a notice with the reason instead.
    /// </summary>
    public static void Start(GameServices s, ReplayEntry replay)
    {
        if (!replay.Readable)
        {
            s.Screens.Push(new NoticeScreen(s, "Replay", replay.Summary));
            return;
        }

        try
        {
            s.Screens.Push(new ReplayViewerScreen(s, replay.Path, ReplayPlaybackLoader.Open(replay.Path, s.RulesDir)));
        }
        catch (SaveLoadException e)
        {
            s.Screens.Push(new NoticeScreen(s, "Cannot play replay", e.Message));
        }
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter() => PlayerColours.Assign(new[] { 0, 1 });

    /// <inheritdoc />
    public void Exit() => PlayerColours.ResetAssignment();

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        if (input.ConsumePressed(Keys.Escape))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        ReplayPlaybackClock clock = _play.Clock;
        if (input.ConsumePressed(Keys.Space)) clock.TogglePause();
        if (input.ConsumePressed(Keys.D1)) clock.SetSpeed(1);
        if (input.ConsumePressed(Keys.D2)) clock.SetSpeed(2);
        if (input.ConsumePressed(Keys.D3)) clock.SetSpeed(4);
        if (input.ConsumePressed(Keys.D4)) clock.SetSpeed(8);
        if (input.ConsumePressed(Keys.OemPlus)) clock.ChangeSpeed(1);
        if (input.ConsumePressed(Keys.OemMinus)) clock.ChangeSpeed(-1);
        if (input.ConsumePressed(Keys.OemPeriod)) _play.StepOne();
        if (input.ConsumePressed(Keys.F) || input.ConsumePressed(Keys.PageDown)) _play.ScrubForward();
        if (input.ConsumePressed(Keys.F2)) _play.CycleViewer();
        if (input.ConsumePressed(Keys.F3)) _overlays.Occupancy = !_overlays.Occupancy;
        if (input.ConsumePressed(Keys.G)) _s.Screens.Push(GraphsScreen.For(_s, _sampler.Timeline));   // M10-17
        if (input.ConsumePressed(Keys.R))
        {
            Restart();
            return;
        }

        Vector2 keyDirection = Vector2.Zero;
        if (input.IsDown(Keys.Left)) keyDirection.X -= 1f;
        if (input.IsDown(Keys.Right)) keyDirection.X += 1f;
        if (input.IsDown(Keys.Up)) keyDirection.Y -= 1f;
        if (input.IsDown(Keys.Down)) keyDirection.Y += 1f;
        if (keyDirection != Vector2.Zero)
        {
            keyDirection.Normalize();
            _camera.PanScreen(keyDirection * PanScreenPixelsPerSecond * (float)t.ElapsedGameTime.TotalSeconds);
        }
        if (input.MiddleDragging) _camera.PanScreen(-input.MouseDelta.ToVector2());
        if (input.WheelDelta != 0) _camera.ZoomBy(input.WheelDelta > 0 ? 1 : -1);

        _play.Advance(t.ElapsedGameTime.Ticks / 10);   // ticks are 100 ns; microseconds = ticks / 10
        _sampler.Observe(_play.Sim);   // M10-17

        var view = new Viewport(0, 0, _s.Viewport.Width, _s.Viewport.Height);
        if (!_centred)
        {
            _centred = true;
            Vector2 mid = Camera.CentreOf(new Rts.Sim.Core.CellCoord(_play.Current.MapWidth / 2, _play.Current.MapHeight / 2));
            _camera.PanScreen((mid - _camera.WorldFromScreen(new Point(view.Width / 2, view.Height / 2))) * _camera.Zoom);
        }
        _camera.Clamp(_play.Current.MapWidth, _play.Current.MapHeight, view);
        UnitInterpolator.Build(_play.Current, _play.Previous, _play.Clock.Alpha, _renderUnits);

        // Read-only: whatever the mouse or keys did above, nothing below the viewer may react.
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        var frame = new SceneFrame(_play.Current, _renderUnits, _play.Projectiles, _play.Sim, _play.Sim.Setup.Rules, _overlays, _selection, null);
        _s.Scene.Draw(sb, _s.Pixel, frame, _camera, _s.Viewport);

        var scale = _s.UiScale;
        ReplayPlaybackClock clock = _play.Clock;
        string state = clock.Ended ? "END" : clock.Paused ? "PAUSED" : $"{clock.Speed}x";
        string view = _play.Viewer < 0 ? "all" : "player " + _play.Viewer;
        var lines = new List<string>
        {
            $"REPLAY  {state}   tick {clock.Tick} / {clock.FinalTick} ({Percent(clock.Progress)})   view {view}",
            "Space pause  1-4 speed  . step  F skip 10s  F2 view  G graphs  R restart  Esc back",
        };
        if (_play.ShowDesyncWarning) lines.Add($"WARNING: the replay no longer matches the game data (differs from tick {_play.DesyncTick})");
        if (_play.IsMissionReplay) lines.Add(Rts.Scripting.MissionReplay.WatchableOnlyNote);   // S18 GS-03 (owner): watchable only
        Hud.Panel(sb, _s.Pixel, new Point(8, 8), lines, scale);
        DrawProgressBar(sb, scale);
        if (clock.Ended) DrawEndBanner(sb, scale);
    }

    private static string Percent(float progress) => FormattableString.Invariant($"{(int)(progress * 100)}%");

    private void DrawProgressBar(SpriteBatch sb, UiUnit scale)
    {
        int height = Math.Max(4, scale * 2);
        var bar = new Rectangle(0, _s.Viewport.Height - height, _s.Viewport.Width, height);
        Shapes.Fill(sb, _s.Pixel, bar, new Color(0, 0, 0, 160));
        Shapes.Fill(sb, _s.Pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _play.Clock.Progress), bar.Height), new Color(0x7B, 0xC4, 0x6A));
    }

    private void DrawEndBanner(SpriteBatch sb, UiUnit scale)
    {
        string[] lines = { "End of replay", "R plays it again, Esc goes back" };
        int width = lines.Max(l => BitmapFont.Measure(l, scale)) + 32;
        int height = Hud.HeightOf(lines.Length, scale) + 16;
        var origin = new Point((_s.Viewport.Width - width) / 2, (_s.Viewport.Height - height) / 2);
        Shapes.Fill(sb, _s.Pixel, new Rectangle(origin.X - 4, origin.Y - 4, width + 8, height + 8), new Color(0, 0, 0, 120));
        Hud.Panel(sb, _s.Pixel, new Point(origin.X + 8, origin.Y + 8), lines, scale);
    }

    // Starts the same file over; if it cannot be opened again the viewer simply stays where it is.
    private void Restart()
    {
        try
        {
            _play = ReplayPlaybackLoader.Open(_path, _s.RulesDir);
        }
        catch (SaveLoadException e)
        {
            _s.Screens.Push(new NoticeScreen(_s, "Cannot play replay", e.Message));
        }
    }
}
