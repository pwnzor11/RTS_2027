// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>The About page: version, the GPL v3 notice and the third-party credits, in a scrolling panel.</summary>
public sealed class AboutScreen : IScreen
{
    private static readonly Color PanelColour = new(0x12, 0x14, 0x10, 238);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color TextColour = new(0xDC, 0xDE, 0xD0);
    private static readonly Color HeadingColour = new(0xE8, 0xA8, 0x28);

    private readonly GameServices _s;
    private readonly IReadOnlyList<string> _credits;
    private List<(string Text, bool Heading)> _lines = new();
    private int _wrappedFor = -1;
    private int _scroll;
    private int _visibleLines = 10;

    /// <summary>Creates the page. <paramref name="creditsMarkdown"/> overrides the credits file text (tests); null reads docs/CREDITS.md if present.</summary>
    public AboutScreen(GameServices services, string? creditsMarkdown = null)
    {
        _s = services;
        _credits = AboutContent.ParseCredits(creditsMarkdown ?? ReadCredits(services.RulesDir));
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit()
    {
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Enter) || input.ConsumePressed(Keys.Back))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        int max = Math.Max(0, _lines.Count - _visibleLines);
        if (input.ConsumePressed(Keys.Down)) _scroll++;
        if (input.ConsumePressed(Keys.Up)) _scroll--;
        if (input.ConsumePressed(Keys.PageDown)) _scroll += _visibleLines - 1;
        if (input.ConsumePressed(Keys.PageUp)) _scroll -= _visibleLines - 1;
        if (input.ConsumePressed(Keys.Home)) _scroll = 0;
        if (input.ConsumePressed(Keys.End)) _scroll = max;
        if (input.WheelDelta != 0) { _scroll -= Math.Sign(input.WheelDelta) * 3; input.ConsumeWheel(); }
        _scroll = Math.Clamp(_scroll, 0, max);
        if (input.LeftPressed && BackRect(_s.Viewport, _s.UiScale).Contains(input.MousePosition))
        {
            input.ConsumeLeft();
            _s.Screens.Pop();
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        int scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int line = BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;
        int width = Math.Min(vp.Width - 8 * scale, 110 * scale * BitmapFont.Advance / 2);
        int pad = 5 * scale;
        int height = vp.Height - 16 * scale;
        var panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        Shapes.Fill(sb, _s.Pixel, panel, PanelColour);
        Shapes.Outline(sb, _s.Pixel, panel, BorderColour, Math.Max(1, scale / 2));

        int textW = width - 2 * pad;
        Wrap(textW, scale);
        int top = panel.Y + pad + line + 2 * scale;
        BitmapFont.Draw(sb, _s.Pixel, $"About RTS_2027   version {BuildInfo.Version}", new Vector2(panel.X + pad, panel.Y + pad), TitleColour, scale, FontWeight.Bold);
        int bottom = panel.Bottom - pad - line - scale;
        _visibleLines = Math.Max(3, (bottom - top) / line);
        for (int i = 0; i < _visibleLines && _scroll + i < _lines.Count; i++)
        {
            (string text, bool heading) = _lines[_scroll + i];
            BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(panel.X + pad, top + i * line), heading ? HeadingColour : TextColour, scale,
                heading ? FontWeight.Bold : FontWeight.Regular);
        }

        string hint = _lines.Count > _visibleLines ? "Up/Down or wheel to scroll    Esc to go back" : "Esc to go back";
        BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(panel.X + pad, panel.Bottom - pad - line + scale), new Color(0x90, 0x94, 0x86), scale);
        Rectangle back = BackRect(vp, scale);
        Shapes.Fill(sb, _s.Pixel, back, new Color(0x2A, 0x2E, 0x26));
        Shapes.Outline(sb, _s.Pixel, back, BorderColour);
        BitmapFont.Draw(sb, _s.Pixel, "Back", new Vector2(back.X + 3 * scale, back.Y + scale), TextColour, scale);
    }

    private static Rectangle BackRect(Viewport vp, int scale)
    {
        int line = BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;
        int width = Math.Min(vp.Width - 8 * scale, 110 * scale * BitmapFont.Advance / 2);
        int height = vp.Height - 16 * scale;
        var panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        return new Rectangle(panel.Right - 5 * scale - 12 * scale, panel.Bottom - 5 * scale - line, 12 * scale, line);
    }

    private void Wrap(int width, int scale)
    {
        if (_wrappedFor == width) return;
        _wrappedFor = width;
        int Measure(string t) => BitmapFont.Measure(t, scale);
        var lines = new List<(string, bool)> { ("License", true) };
        lines.AddRange(TextWrap.Wrap(AboutContent.Notice, width, Measure).Select(l => (l, false)));
        lines.Add(("", false));
        lines.Add(("Third-party software and assets", true));
        foreach (string credit in _credits) lines.AddRange(TextWrap.Wrap(credit, width, Measure).Select(l => (l, false)));
        lines.Add(("", false));
        lines.Add(("Full notices: THIRD_PARTY_NOTICES.md next to the game.", false));
        _lines = lines;
    }

    // docs/CREDITS.md exists in a source checkout (two folders above data/); a published build does not carry it.
    private static string? ReadCredits(string rulesDir)
    {
        try
        {
            string? data = Path.GetDirectoryName(rulesDir);
            string? root = data is null ? null : Path.GetDirectoryName(data);
            string file = root is null ? "" : Path.Combine(root, "docs", "CREDITS.md");
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
