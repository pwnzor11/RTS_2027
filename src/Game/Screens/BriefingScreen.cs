// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private readonly List<string> _lines = new();
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
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0) { _lines.Add(""); continue; }
            _lines.AddRange(MissionOverlay.Wrap(line, WrapChars));
        }

        if (model.Objectives.Count > 0)
        {
            _lines.Add("");
            _lines.Add("OBJECTIVES");
            foreach (string o in model.Objectives) _lines.AddRange(MissionOverlay.Wrap("- " + o, WrapChars));
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
    public void Enter() => _still = LoadStill();

    /// <inheritdoc />
    public void Exit()
    {
        _still?.Dispose();
        _still = null;
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
            return;
        }

        _panel.Update(input, ButtonViewport(), _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        int scale = _s.UiScale;
        int lineHeight = BitmapFont.LineHeight(scale, FontWeight.Regular) + 4;
        int x = Math.Max(16, (_s.Viewport.Width - BitmapFont.Measure(new string('M', WrapChars), scale)) / 2);
        int y = 24;
        BitmapFont.Draw(sb, _s.Pixel, _model.Title.ToUpperInvariant(), new Vector2(x, y), BriefingModel.ThemeOf(_model.Faction), scale);
        y += lineHeight * 2;
        if (_still != null)
        {
            sb.Draw(_still, new Rectangle(x, y, TextWidth(scale), StillHeight(scale)), Color.White);
            y += StillHeight(scale) + lineHeight;
        }

        int remaining = _shown;
        foreach (string line in _lines)
        {
            if (remaining <= 0) break;
            string visible = line.Length <= remaining ? line : line[..remaining];
            BitmapFont.Draw(sb, _s.Pixel, visible, new Vector2(x, y), TextColour, scale);
            remaining -= line.Length + 1;
            y += lineHeight;
        }

        if (Complete) _panel.Draw(sb, _s.Pixel, ButtonViewport(), scale, _mouse);
    }

    // The menu panel centres itself in the viewport it is given: give it the strip below the text (and the still, when there is one).
    private Viewport ButtonViewport()
    {
        int lineHeight = BitmapFont.LineHeight(_s.UiScale, FontWeight.Regular) + 4;
        int stillBlock = _still == null ? 0 : StillHeight(_s.UiScale) + lineHeight;
        int top = Math.Min(_s.Viewport.Height - 120, 24 + lineHeight * (_lines.Count + 3) + stillBlock);
        return new Viewport(0, top, _s.Viewport.Width, Math.Max(100, _s.Viewport.Height - top));
    }

    // The text column's width: the still is scaled to it, keeping the 16:9 aspect of the image.
    private static int TextWidth(int scale) => BitmapFont.Measure(new string('M', WrapChars), scale);

    private int StillHeight(int scale) => _still == null ? 0 : TextWidth(scale) * _still.Height / _still.Width;

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
