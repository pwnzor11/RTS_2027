// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Lan;

/// <summary>
/// The in-game LAN overlay (M9-08, M9-10) drawn by the game screen over a LAN match: one centred panel at a time (host gone, desync,
/// pause with its countdown, or waiting with the host's Drop buttons), the fading chat list with each sender's colour, and the one-line
/// chat input. It reads a <see cref="LanSession"/> and returns the button clicked; the screen acts on it.
/// </summary>
public sealed class LanOverlay
{
    /// <summary>How long a chat line stays fully visible, in microseconds.</summary>
    public const long ChatShowUs = 10_000_000;

    /// <summary>How long a chat line then takes to fade out, in microseconds.</summary>
    public const long ChatFadeUs = 2_000_000;

    /// <summary>Most chat lines shown at once.</summary>
    public const int ChatRows = 8;

    private static readonly Color PanelColour = new(0, 0, 0, 200);
    private static readonly Color TextColour = new(0xF0, 0xE6, 0xC0);
    private static readonly Color NoticeColour = new(0xF0, 0xC0, 0x60);
    private static readonly Color SpectatorColour = new(0xC8, 0xC8, 0xC8);
    private static readonly Color ButtonColour = new(0xF0, 0xB0, 0x40);

    private readonly KeyRepeat _backRepeat = new();
    private readonly List<string> _lines = new();
    private readonly List<(string Label, LanOverlayAction Action)> _buttonDefs = new();
    private readonly List<(Rectangle Rect, string Label, LanOverlayAction Action)> _buttons = new();
    private Rectangle _panel;

    /// <summary>The chat input line.</summary>
    public LanChatInput ChatInput { get; } = new();

    /// <summary>Handles the chat keys. Returns true while the input line is open (the caller then keeps the game hotkeys quiet).</summary>
    public bool UpdateChat(InputFrame input, LanSession session)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ChatInput.Typing)
        {
            if (input.ConsumePressed(GameAction.Talk)) ChatInput.Open();
            return ChatInput.Typing;
        }
        if (input.ConsumePressed(Keys.Escape))
        {
            ChatInput.Cancel();
            return false;
        }
        if (input.ConsumePressed(Keys.Enter))
        {
            if (ChatInput.Submit() is { } line) session.SendChat(line.Team, line.Text);
            return false;
        }
        if (input.ConsumePressed(Keys.Tab)) ChatInput.ToggleAudience();
        bool backHeld = input.IsDown(Keys.Back);
        input.ConsumePressed(Keys.Back);
        if (_backRepeat.Step(backHeld)) ChatInput.Backspace();
        foreach (char ch in input.ConsumeText()) ChatInput.Type(ch);
        return true;
    }

    /// <summary>Lays out the panel and returns the button clicked this frame (the click is then consumed), or null.</summary>
    /// <param name="input">This frame's input.</param>
    /// <param name="session">The match.</param>
    /// <param name="map">The map view rectangle the panel centres in.</param>
    /// <param name="scale">The UI unit.</param>
    public LanOverlayAction? Update(InputFrame input, LanSession session, Rectangle map, UiUnit scale)
    {
        ArgumentNullException.ThrowIfNull(input);
        Layout(session, map, scale);
        if (_panel.IsEmpty) return null;
        if (input.LeftPressed && _panel.Contains(input.MousePosition))
        {
            input.ConsumeLeft();
            foreach ((Rectangle rect, _, LanOverlayAction action) in _buttons)
                if (rect.Contains(input.MousePosition)) return action;
        }
        return null;
    }

    /// <summary>True when the point is over the panel (the map cursor and clicks keep off it).</summary>
    public bool HitTest(Point p) => !_panel.IsEmpty && _panel.Contains(p);

    /// <summary>Draws the panel, the chat list and the input line.</summary>
    /// <param name="sb">The sprite batch.</param>
    /// <param name="pixel">The 1x1 white texture.</param>
    /// <param name="session">The match.</param>
    /// <param name="map">The map view rectangle.</param>
    /// <param name="scale">The UI unit.</param>
    /// <param name="mouse">The mouse position (button hover).</param>
    /// <param name="chatBottomLeft">Where the chat list's bottom-left corner sits.</param>
    public void Draw(SpriteBatch sb, Texture2D pixel, LanSession session, Rectangle map, UiUnit scale, Point mouse, Point chatBottomLeft)
    {
        Layout(session, map, scale);
        DrawChat(sb, pixel, session, scale, chatBottomLeft, map.Width - 16);
        if (_panel.IsEmpty) return;
        Shapes.Fill(sb, pixel, _panel, PanelColour);
        Shapes.Outline(sb, pixel, _panel, ButtonColour, 2);
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale;
        int y = _panel.Y + 6 * scale;
        foreach (string line in _lines)
        {
            BitmapFont.Draw(sb, pixel, line, new Vector2(_panel.X + 8 * scale, y), TextColour, scale);
            y += lineH;
        }
        foreach ((Rectangle rect, string label, _) in _buttons)
        {
            Shapes.Fill(sb, pixel, rect, new Color(0x20, 0x20, 0x20, 230));
            Shapes.Outline(sb, pixel, rect, rect.Contains(mouse) ? Color.White : ButtonColour, 2);
            BitmapFont.Draw(sb, pixel, label, new Vector2(rect.X + 6 * scale, rect.Y + 3 * scale), TextColour, scale);
        }
    }

    /// <summary>
    /// The panel's text lines and buttons for the session's state (pure; the tests call it). Priority: host gone, desync or resync,
    /// pause, waiting. Empty when nothing needs saying.
    /// </summary>
    public static void Describe(LanSession session, List<string> lines, List<(string Label, LanOverlayAction Action)> buttons)
    {
        ArgumentNullException.ThrowIfNull(session);
        lines.Clear();
        buttons.Clear();
        if (session.HostGone)
        {
            lines.Add(LanText.HostLeft);
            if (!session.IsSpectator) buttons.Add((LanText.ContinueAgainstAi, new LanOverlayAction(LanOverlayActionKind.ContinueAgainstAi)));
            buttons.Add((LanText.BackToMenu, new LanOverlayAction(LanOverlayActionKind.BackToMenu)));
            return;
        }
        if (session.DesyncTick is int tick || session.Resyncing)
        {
            if (!session.IsHost)
            {
                lines.Add(session.Resyncing ? LanText.Resyncing : LanText.DesyncClient);
                return;
            }
            lines.Add(LanText.DesyncHost.Replace("{tick}", (session.DesyncTick ?? session.Tick).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (session.DesyncReportFolder is { } folder) lines.Add(LanText.DesyncReport.Replace("{folder}", folder));
            if (session.Resyncing)
            {
                lines.Add(LanText.Resyncing);
                return;
            }
            buttons.Add((LanText.ResyncFromHost, new LanOverlayAction(LanOverlayActionKind.Resync)));
            buttons.Add((LanText.Continue, new LanOverlayAction(LanOverlayActionKind.Continue)));
            buttons.Add((LanText.EndMatch, new LanOverlayAction(LanOverlayActionKind.EndMatch)));
            return;
        }
        if (session.Paused)
        {
            int ms = session.ResumeCountdownMs;
            if (ms > 0)
            {
                lines.Add(LanText.ResumingIn.Replace("{seconds}", ((ms + 999) / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                return;
            }
            lines.Add(LanText.PausedBy.Replace("{name}", session.PeerName(Math.Max(0, session.PausedBy))));
            buttons.Add((LanText.Resume, new LanOverlayAction(LanOverlayActionKind.Resume)));
            return;
        }
        if (session.ShowWaiting)
        {
            foreach (int peer in session.WaitingFor)
            {
                string name = session.PeerName(peer);
                lines.Add(LanText.Waiting.Replace("{name}", name).Replace("{time}", LanText.Clock(session.WaitedMs)));
                if (session.CanDrop(peer)) buttons.Add((LanText.Drop.Replace("{name}", name), new LanOverlayAction(LanOverlayActionKind.Drop, peer)));
            }
        }
    }

    private void Layout(LanSession session, Rectangle map, UiUnit scale)
    {
        Describe(session, _lines, _buttonDefs);
        _buttons.Clear();
        if (_lines.Count == 0)
        {
            _panel = Rectangle.Empty;
            return;
        }
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale;
        int buttonH = lineH + 4 * scale;
        int pad = 8 * scale;
        int width = 0;
        foreach (string line in _lines) width = Math.Max(width, BitmapFont.Measure(line, scale));
        int buttonsW = 0;
        foreach ((string label, _) in _buttonDefs) buttonsW += BitmapFont.Measure(label, scale) + 12 * scale + pad;
        width = Math.Max(width, buttonsW - pad) + 2 * pad;
        int height = 6 * scale + _lines.Count * lineH + (_buttonDefs.Count > 0 ? buttonH + 6 * scale : 0) + 4 * scale;
        _panel = new Rectangle(map.X + (map.Width - width) / 2, map.Y + map.Height / 4, width, height);
        int x = _panel.X + pad;
        int by = _panel.Y + 6 * scale + _lines.Count * lineH + 4 * scale;
        foreach ((string label, LanOverlayAction action) in _buttonDefs)
        {
            int w = BitmapFont.Measure(label, scale) + 12 * scale;
            _buttons.Add((new Rectangle(x, by, w, buttonH), label, action));
            x += w + pad;
        }
    }

    private void DrawChat(SpriteBatch sb, Texture2D pixel, LanSession session, UiUnit scale, Point bottomLeft, int maxWidth)
    {
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale;
        int y = bottomLeft.Y;
        if (ChatInput.Typing)
        {
            string prompt = (ChatInput.Team ? LanText.ToTeam : LanText.ToAll) + ChatInput.Text + "_";
            y -= lineH;
            BitmapFont.Draw(sb, pixel, LanText.ChatHint, new Vector2(bottomLeft.X, y), SpectatorColour, scale);
            y -= lineH;
            int w = Math.Min(maxWidth, BitmapFont.Measure(prompt, scale) + 8 * scale);
            Shapes.Fill(sb, pixel, new Rectangle(bottomLeft.X - 4 * scale, y - scale, w, lineH), PanelColour);
            BitmapFont.Draw(sb, pixel, prompt, new Vector2(bottomLeft.X, y), Color.White, scale);
        }

        IReadOnlyList<LanChatLine> chat = session.Chat;
        long now = session.Clock;
        int shown = 0;
        for (int i = chat.Count - 1; i >= 0 && shown < ChatRows; i--)
        {
            LanChatLine line = chat[i];
            long age = now - line.AtMicroseconds;
            float alpha = ChatInput.Typing ? 1f : age <= ChatShowUs ? 1f : 1f - (age - ChatShowUs) / (float)ChatFadeUs;
            if (alpha <= 0f) break;
            shown++;
            y -= lineH;
            byte a = (byte)(255 * Math.Clamp(alpha, 0f, 1f));
            int x = bottomLeft.X;
            if (line.System)
            {
                BitmapFont.Draw(sb, pixel, line.Text, new Vector2(x, y), Shapes.Alpha(NoticeColour, a), scale);
                continue;
            }
            string head = (line.Team ? LanText.TeamTag : "") + line.Sender + ": ";
            Color nameColour = line.Player >= 0 ? PlayerColours.Base(line.Player) : SpectatorColour;
            BitmapFont.Draw(sb, pixel, head, new Vector2(x, y), Shapes.Alpha(nameColour, a), scale);
            BitmapFont.Draw(sb, pixel, line.Text, new Vector2(x + BitmapFont.Measure(head, scale), y), Shapes.Alpha(TextColour, a), scale);
        }
    }
}
