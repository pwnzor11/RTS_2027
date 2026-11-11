// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Ui.Fonts;
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>
/// The commander chat panel (M5-07, design 09 §5): bottom-left above the key help. Each commander line is a speaker header in the
/// player's colour, then the text wrapped to the panel width; lines the player typed read "You -> name:"; system notices are dimmed.
/// Enter opens the input, Enter sends it through <see cref="ICommanderChannel.Hear"/>, Esc cancels. Hidden with no commanders or
/// with the chat setting off.
/// </summary>
public sealed class CommanderChatPanel
{
    /// <summary>Panel width in screen pixels at UI scale 1.</summary>
    public const int WidthPx = 260;

    /// <summary>Most rows drawn above the input line.</summary>
    public const int MaxRows = 12;

    private readonly Rts.Game.Input.KeyRepeat _backRepeat = new();
    private const int Pad = 8;
    private const int LineGap = 4;
    private static readonly Color PanelColour = new(0, 0, 0, 170);
    private static readonly Color BodyColour = new(0xE8, 0xE8, 0xE8);
    private static readonly Color DimColour = new(0x9A, 0x9A, 0x9A);
    private static readonly Color InputColour = new(0xD8, 0xC8, 0x8A);

    private readonly CommanderChatModel _model = new();
    private readonly List<CommanderLine> _visible = new();
    private readonly List<(string Text, Color Colour)> _rows = new();
    private bool _active;
    private int _lastTick;

    /// <summary>The model behind the panel (read by tests and the screen).</summary>
    public CommanderChatModel Model => _model;

    /// <summary>
    /// Handles the chat keys for this frame. Call before the game hotkeys. Returns true while the input line is open: the caller
    /// then calls <see cref="InputFrame.ConsumeKeyboard"/> so the hotkeys stay quiet. An open console cancels the input.
    /// </summary>
    /// <param name="input">This frame's input.</param>
    /// <param name="game">The game's commanders and chat, or null for the sandbox, demo and missions.</param>
    /// <param name="tick">The sim tick the send is stamped with.</param>
    /// <param name="chatOn">Settings.LlmChatOn.</param>
    /// <param name="consoleOpen">True while the debug console has the keyboard.</param>
    public bool Update(InputFrame input, CommanderSet? game, int tick, bool chatOn, bool consoleOpen)
    {
        ArgumentNullException.ThrowIfNull(input);
        IReadOnlyList<ICommanderChannel> commanders = game is not null && chatOn ? game.Commanders : Array.Empty<ICommanderChannel>();
        _active = commanders.Count > 0;
        _lastTick = tick;
        if (game is null || !chatOn || consoleOpen)
        {
            _model.Cancel();
            return false;
        }

        _model.Refresh(game.Chat);
        if (!_model.Typing)
        {
            if (commanders.Count > 0 && input.ConsumePressed(GameAction.Talk)) _model.Open(commanders);
            return _model.Typing;
        }

        if (input.ConsumePressed(Keys.Escape))
        {
            _model.Cancel();
            return false;
        }
        if (input.ConsumePressed(Keys.Enter))
        {
            _model.Submit(commanders, tick);
            return _model.Typing;
        }
        bool backHeld = input.IsDown(Keys.Back);
        input.ConsumePressed(Keys.Back);
        if (_backRepeat.Step(backHeld)) _model.Backspace();   // S18 GAMEUI-11: holding Backspace repeats
        foreach (char ch in input.ConsumeText()) _model.Type(ch);
        return _model.Typing;
    }

    /// <summary>
    /// Draws the panel with its bottom-left corner at <paramref name="bottomLeft"/>. Nothing is drawn when there are no commanders
    /// or the chat is off (the last <see cref="Update"/> saw them).
    /// </summary>
    /// <param name="maxWidth">The widest the panel may be, in screen pixels.</param>
    public void Draw(SpriteBatch sb, Texture2D pixel, UiUnit scale, Point bottomLeft, int maxWidth)
    {
        if (!_active) return;
        _model.VisibleLines(_lastTick, _visible);
        int width = Math.Min(WidthPx * scale, maxWidth);
        int wrapWidth = width - 2 * Pad;
        Func<string, int> measure = text => BitmapFont.Measure(text, scale);
        BuildRows(wrapWidth, measure);

        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGap;
        int height = _rows.Count * lineH - LineGap + Pad;
        var box = new Rectangle(bottomLeft.X, bottomLeft.Y - height, width, height);
        sb.Draw(pixel, box, PanelColour);
        int y = box.Y + Pad / 2;
        foreach (var (text, colour) in _rows)
        {
            BitmapFont.Draw(sb, pixel, text, new Vector2(box.X + Pad, y), colour, scale);
            y += lineH;
        }
    }

    private void BuildRows(int wrapWidth, Func<string, int> measure)
    {
        _rows.Clear();
        foreach (CommanderLine line in _visible)
        {
            if (line.System)
            {
                foreach (string row in CommanderChatModel.Wrap(line.Text, wrapWidth, measure)) _rows.Add((row, DimColour));
                continue;
            }
            string header = line.FromPlayer ? CommanderChatText.YouTo + line.Speaker + ":" : line.Speaker + ":";
            _rows.Add((header, PlayerColours.Base(line.Player)));
            foreach (string row in CommanderChatModel.Wrap(line.Text, wrapWidth, measure)) _rows.Add((row, BodyColour));
        }
        if (_rows.Count > MaxRows) _rows.RemoveRange(0, _rows.Count - MaxRows);

        if (_model.Typing)
        {
            _rows.Add((CommanderChatText.Prompt + _model.Input + "_", InputColour));
            if (_model.Notice is { } notice) _rows.Add((notice, InputColour));
            _rows.Add((CommanderChatText.InputHint, DimColour));
        }
        else if (_rows.Count == 0)
        {
            _rows.Add((CommanderChatText.OpenHint, DimColour));
        }
    }
}
