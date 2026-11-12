// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Text-first mission briefing (D-030): the briefing file typed out like a teletype (Space or a click on the text shows it all),
/// then Start (opens the mission) and Back. Esc goes back.
/// </summary>
public sealed class BriefingScreen : IScreen
{
    private const int WrapChars = 84;
    private const int CharsPerUpdate = 3;
    private static readonly Color TextColour = new(0xB8, 0xE0, 0xA0);

    private readonly GameServices _s;
    private readonly BriefingModel _model;
    private readonly List<MdLine> _lines = new();
    private int _scroll;
    private readonly int _totalChars;
    private readonly MenuPanel _panel;
    private Texture2D? _still;
    private int _shown;
    private Point _mouse;

    /// <summary>Creates the classic screen (no difficulty choice) from the mission's folder under data/missions.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="missionId">Mission folder name.</param>
    /// <param name="start">Builds the mission's game screen when Start is chosen.</param>
    public BriefingScreen(GameServices services, string missionId, Func<IScreen> start)
        : this(services, LoadClassic(services, missionId), _ => start(), false)
    {
    }

    /// <summary>Creates the campaign briefing v2 (plan 3.2): title in the faction theme, text, objectives preview, difficulty choice and Start.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="missionId">Mission folder name.</param>
    /// <param name="start">Builds the mission's game screen for the chosen tier (1..5).</param>
    /// <param name="faction">The campaign faction (theme).</param>
    /// <param name="tier">The tier the difficulty row starts on.</param>
    public BriefingScreen(GameServices services, string missionId, Func<int, IScreen> start, string faction, int tier)
        : this(services, BriefingModel.Load(CampaignFlow.DataDir(services), services.RulesDir, missionId, faction, tier,
            CampaignFlow.LoadCatalog(services), Archive.ArchiveProgress.Load()), start, true)
    {
    }

    private BriefingScreen(GameServices services, BriefingModel model, Func<int, IScreen> start, bool chooseDifficulty)
    {
        _s = services;
        _model = model;
        foreach (string raw in model.Text.Split('\n'))
            _lines.AddRange(MarkdownLines.Wrap(MarkdownLines.Parse(raw), WrapChars));

        if (model.Objectives.Count > 0)
        {
            _lines.Add(MdLine.Blank);
            _lines.Add(new MdLine(true, new[] { new MdSpan("OBJECTIVES", true) }));
            foreach (string o in model.Objectives) _lines.AddRange(MarkdownLines.Wrap(MarkdownLines.Parse("- " + o), WrapChars));
        }

        _totalChars = _lines.Sum(l => l.Length + 1);
        var items = new List<MenuItem>();
        if (chooseDifficulty)
            items.Add(new MenuItem(() => "Difficulty: " + _model.TierName, null, d => _model.AdjustTier(d),
                new MenuButton("-", () => _model.AdjustTier(-1)), new MenuButton("+", () => _model.AdjustTier(1))));
        items.Add(new MenuItem(() => "Start mission", () => _s.Screens.Push(start(_model.Tier))));
        items.Add(new MenuItem(() => "Back", () => _s.Screens.Pop()));
        _panel = new MenuPanel(model.Title, items) { Selected = chooseDifficulty ? 1 : 0 };
    }

    private static BriefingModel LoadClassic(GameServices services, string missionId)
    {
        string folder = MissionLoader.FolderOf(CampaignFlow.DataDir(services), missionId);
        MissionDef def = MissionLoader.Load(folder);
        return new BriefingModel(def.Title, def.Faction, File.ReadAllText(Path.Combine(folder, def.BriefingFile)), Array.Empty<string>(),
            BriefingModel.LoadTierNames(services.RulesDir), 3);
    }

    /// <summary>The difficulty choice and content shown.</summary>
    public BriefingModel Model => _model;

    /// <summary>True once the whole text is shown.</summary>
    public bool Complete => _shown >= _totalChars;

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
        _still = LoadStill();
        AudioBootstrap.Director?.RequestMusic(MusicState.Briefing, AudioDirector.FactionOf(_model.Faction));
    }

    /// <inheritdoc />
    public void Exit()
    {
        _still?.Dispose();
        _still = null;
        AudioBootstrap.Director?.ReleaseMusic();
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Pop();
            return;
        }

        if (!Complete)
        {
            _shown += CharsPerUpdate;
            if (input.ConsumePressed(Keys.Space) || input.LeftPressed) { _shown = _totalChars; input.ConsumeLeft(); }
            _scroll = Math.Max(0, TypedLines() - VisibleLines());
            return;
        }

        if (input.WheelDelta != 0) _scroll -= Math.Sign(input.WheelDelta) * 3;
        if (input.ConsumePressed(Keys.PageDown)) _scroll += VisibleLines() - 1;
        if (input.ConsumePressed(Keys.PageUp)) _scroll -= VisibleLines() - 1;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lines.Count - VisibleLines()));
        _panel.Update(input, ButtonViewport(), _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        var scale = _s.UiScale;
        int lineHeight = LineHeight();
        int x = TextX();
        int y = 24;
        BitmapFont.Draw(sb, _s.Pixel, _model.Title.ToUpperInvariant(), new Vector2(x, y), BriefingModel.ThemeOf(_model.Faction), scale);
        y += lineHeight * 2;
        if (_still != null)
        {
            sb.Draw(_still, new Rectangle(x, y, StillHeight(scale) * _still.Width / _still.Height, StillHeight(scale)), Color.White);
        }

        int remaining = _shown;
        int visible = VisibleLines();
        Color theme = BriefingModel.ThemeOf(_model.Faction);
        for (int i = 0; i < _lines.Count && remaining > 0; i++)
        {
            MdLine line = _lines[i];
            int lineY = TextTop() + (i - _scroll) * lineHeight;
            if (i >= _scroll && i < _scroll + visible) DrawLine(sb, line, remaining, x, lineY, scale, theme);
            remaining -= line.Length + 1;
        }

        int maxScroll = Math.Max(0, _lines.Count - visible);
        if (maxScroll > 0)
        {
            var track = new Rectangle(x + TextWidth(scale) + 8, TextTop(), 4, visible * lineHeight);
            Shapes.Fill(sb, _s.Pixel, track, new Color(0x2A, 0x2E, 0x26));
            ControlsThumb(sb, track, visible, maxScroll);
        }

        if (Complete) _panel.Draw(sb, _s.Pixel, ButtonViewport(), scale, _mouse);
    }

    private void ControlsThumb(SpriteBatch sb, Rectangle track, int visible, int maxScroll)
    {
        Rectangle thumb = ControlsScreen.ThumbRect(track, _lines.Count, visible, _scroll);
        Shapes.Fill(sb, _s.Pixel, thumb, new Color(0x6A, 0x72, 0x60));
    }

    private void DrawLine(SpriteBatch sb, MdLine line, int shownChars, int x, int y, UiUnit scale, Color theme)
    {
        float px = x;
        foreach (MdSpan span in line.Spans)
        {
            if (shownChars <= 0) break;
            string text = span.Text.Length <= shownChars ? span.Text : span.Text[..shownChars];
            shownChars -= span.Text.Length;
            FontWeight weight = span.Bold ? FontWeight.Bold : FontWeight.Regular;
            BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(px, y), line.Heading ? theme : TextColour, scale, weight);
            px += BitmapFont.Measure(text, scale, weight);
        }
    }

    private int LineHeight() => BitmapFont.LineHeight(_s.UiScale, FontWeight.Regular) + 4;

    private int TextX() => Math.Max(16, (_s.Viewport.Width - TextWidth(_s.UiScale)) / 2);

    // Below the title and the still (when there is one).
    private int TextTop() => 24 + LineHeight() * 2 + (_still == null ? 0 : StillHeight(_s.UiScale) + LineHeight());

    // The strip for Start/Back sits at the bottom edge; the text area is everything between the heading block and that strip.
    private int ButtonsTop() => _s.Viewport.Height - _panel.HeightAt(_s.UiScale) - 8;

    private int VisibleLines() => Math.Max(2, (ButtonsTop() - 8 - TextTop()) / LineHeight());

    private int TypedLines()
    {
        int n = 0, left = _shown;
        foreach (MdLine l in _lines)
        {
            if (left <= 0) break;
            n++;
            left -= l.Length + 1;
        }

        return n;
    }

    // The menu panel centres itself in the viewport it is given: give it exactly the strip at the bottom.
    private Viewport ButtonViewport()
    {
        _panel.OffsetY = ButtonsTop();
        return new Viewport(0, 0, _s.Viewport.Width, _panel.HeightAt(_s.UiScale));
    }

    // The text column's width: the still is scaled to it, keeping the 16:9 aspect of the image.
    private static int TextWidth(UiUnit scale) => BitmapFont.Measure(new string('M', WrapChars), scale);

    // At most a quarter of the window, so the text and the Start/Back strip still fit on short windows (width follows the aspect).
    private int StillHeight(UiUnit scale) => _still == null ? 0 : Math.Min(TextWidth(scale) * _still.Height / _still.Width, _s.Viewport.Height / 4);

    // A missing or unreadable still means no image at all (the briefing is laid out as without one).
    private Texture2D? LoadStill()
    {
        if (_model.Still is not { Length: > 0 } id) return null;
        try
        {
            string path = Path.Combine(CampaignFlow.DataDir(_s), "images", "stills", id + ".png");
            if (!File.Exists(path)) return null;
            using FileStream fs = File.OpenRead(path);
            return Texture2D.FromStream(_s.Graphics, fs);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
