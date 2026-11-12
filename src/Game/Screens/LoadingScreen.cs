// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>
/// The loading screen (M10-20): shown while a skirmish or campaign mission builds. It draws one frame first, then does the work (so the window shows
/// something instead of freezing on the old screen), then replaces itself with the built screen. It shows an act or faction still when there is one, a
/// bar (real progress when the load has several steps, a moving bar when it has one) and a tip from <c>data/ui/tips.toml</c>.
/// A data error while building pops the screen and calls <c>onDataError</c>; any other exception is rethrown.
/// </summary>
public sealed class LoadingScreen : IScreen
{
    private static readonly Color Panel = new(0x0E, 0x10, 0x0C);
    private static readonly Color TextColour = new(0xDC, 0xDE, 0xD0);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color BarBack = new(0x1C, 0x20, 0x18);
    private static readonly Color BarFill = new(0xE8, 0xA8, 0x28);
    private static readonly Color Border = new(0x6A, 0x72, 0x60);

    private readonly GameServices _s;
    private readonly string _title;
    private readonly string _subtitle;
    private readonly LoadingModel _model;
    private readonly string? _artPath;
    private readonly LoadingTip? _tip;
    private readonly Action<Exception>? _onDataError;
    private Texture2D? _art;
    private bool _drawn;
    private int _frames;

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="title">Heading, e.g. "Loading".</param>
    /// <param name="subtitle">The map or mission name, or "".</param>
    /// <param name="model">The work.</param>
    /// <param name="artPath">A PNG to draw behind the text, or null.</param>
    /// <param name="tip">The tip to show, or null.</param>
    /// <param name="onDataError">Called after the screen has left the stack when the build threw a data error (see <see cref="ScreenGuard.IsDataError"/>); null shows a Notice.</param>
    public LoadingScreen(GameServices services, string title, string subtitle, LoadingModel model, string? artPath, LoadingTip? tip, Action<Exception>? onDataError = null)
    {
        _s = services;
        _title = title;
        _subtitle = subtitle;
        _model = model;
        _artPath = artPath;
        _tip = tip;
        _onDataError = onDataError;
    }

    /// <summary>
    /// A loading screen for one build: picks a tip for <paramref name="factions"/> and a still (<paramref name="stillId"/> or a faction still).
    /// </summary>
    /// <param name="services">Shared services.</param>
    /// <param name="subtitle">The map or mission name, or "".</param>
    /// <param name="factions">Faction ids in the game (see <see cref="LoadingArt.IdOf"/>), for faction tips and the picture.</param>
    /// <param name="stillId">A mission's act still id, or null.</param>
    /// <param name="build">Builds the game screen.</param>
    /// <param name="onDataError">See the constructor.</param>
    public static LoadingScreen For(GameServices services, string subtitle, IReadOnlyList<string> factions, string? stillId, Func<IScreen> build,
        Action<Exception>? onDataError = null)
    {
        int seed = Random.Shared.Next();   // the look of a loading screen is not part of the game, so it is not seeded
        string dataDir = Path.GetFullPath(Path.Combine(services.RulesDir, ".."));
        LoadingTip? tip = LoadingTips.Pick(LoadingTips.Load(services.RulesDir), factions, seed);
        string? art = LoadingArt.Choose(dataDir, factions.Count > 0 ? factions[0] : null, stillId, seed);
        return new LoadingScreen(services, "Loading", subtitle, LoadingModel.Single("Building the game", build), art, tip, onDataError);
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <summary>The model (tests).</summary>
    public LoadingModel Model => _model;

    /// <inheritdoc />
    public void Enter() => _art = LoadArt();

    /// <inheritdoc />
    public void Exit()
    {
        _art?.Dispose();
        _art = null;
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _frames++;
        if (!_drawn) return;   // the first frame is only drawn
        _model.Advance();
        if (_model.Failed is { } failure)
        {
            if (!ScreenGuard.IsDataError(failure)) ExceptionDispatchInfo.Capture(failure).Throw();
            _s.Screens.Post(() =>
            {
                _s.Screens.Pop();
                if (_onDataError is not null) _onDataError(failure);
                else _s.Screens.Push(new NoticeScreen(_s, "Cannot start", ScreenGuard.Describe(failure)));
            });
            return;
        }

        if (_model.Result is { } result) _s.Screens.Replace(result);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        _drawn = true;
        Viewport vp = _s.Viewport;
        UiUnit scale = _s.UiScale;
        sb.Draw(_s.Pixel, new Rectangle(0, 0, vp.Width, vp.Height), Panel);
        if (_art is not null)
        {
            // Cover the window, keeping the shape of the picture.
            float k = Math.Max(vp.Width / (float)_art.Width, vp.Height / (float)_art.Height);
            int w = (int)(_art.Width * k), h = (int)(_art.Height * k);
            sb.Draw(_art, new Rectangle((vp.Width - w) / 2, (vp.Height - h) / 2, w, h), Color.White);
            sb.Draw(_s.Pixel, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 110));
        }

        int line = BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;
        int margin = 12 * scale;
        int barW = Math.Min(vp.Width - 2 * margin, 140 * scale);
        int barH = 4 * scale;
        int barX = (vp.Width - barW) / 2;
        int barY = vp.Height - margin - barH - 4 * line;

        Text(sb, _title, barX, barY - 3 * line - scale, TitleColour, scale, FontWeight.Bold);
        if (_subtitle.Length > 0) Text(sb, _subtitle, barX, barY - 2 * line, TextColour, scale, FontWeight.Regular);

        var bar = new Rectangle(barX, barY, barW, barH);
        Shapes.Fill(sb, _s.Pixel, bar, BarBack);
        if (_model.HasProgress)
        {
            Shapes.Fill(sb, _s.Pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _model.Fraction), bar.Height), BarFill);
        }
        else
        {
            int seg = bar.Width / 4;
            int travel = bar.Width - seg;
            int phase = _frames * 3 % (2 * travel);
            int x = phase < travel ? phase : 2 * travel - phase;
            Shapes.Fill(sb, _s.Pixel, new Rectangle(bar.X + x, bar.Y, seg, bar.Height), BarFill);
        }

        Shapes.Outline(sb, _s.Pixel, bar, Border);
        string label = _model.CurrentLabel;
        if (label.Length > 0) Text(sb, label, barX, barY + barH + scale, TextColour, scale, FontWeight.Regular);

        if (_tip is not null)
        {
            int y = barY + barH + 2 * line;
            foreach (string wrapped in TextWrap.Wrap("Tip: " + _tip.Text, barW, text => BitmapFont.Measure(text, scale)))
            {
                Text(sb, wrapped, barX, y, TextColour, scale, FontWeight.Regular);
                y += line;
            }
        }
    }

    private void Text(SpriteBatch sb, string text, int x, int y, Color colour, UiUnit scale, FontWeight weight)
    {
        // A shadow keeps the text readable over the picture.
        BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(x + 1, y + 1), Color.Black, scale, weight);
        BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(x, y), colour, scale, weight);
    }

    private Texture2D? LoadArt()
    {
        if (_artPath is null) return null;
        try
        {
            using FileStream fs = File.OpenRead(_artPath);
            return Texture2D.FromStream(_s.Graphics, fs);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
