// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Ui;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Mission mode HUD (D-061): the objectives panel under the tick panel at the top left, and the message ticker (Media.Message lines
/// younger than <see cref="MessageShowTicks"/>) above the key help at the bottom left. Dialogue (M8-05) shows one line above the ticker.
/// Reads the <see cref="MissionHost"/> only.
/// </summary>
internal sealed class MissionOverlay
{
    /// <summary>How long a message stays on screen, in ticks (30 seconds).</summary>
    public const int MessageShowTicks = 600;

    private const int WrapChars = 78;
    private const int MaxTickerLines = 6;
    private const int GapPx = 6;

    private readonly MissionHost _host;
    private readonly MissionDialoguePresenter _dialogue = new();
    private readonly MissionCameraCue _cameraCue = new();
    private int _tick;

    public MissionOverlay(MissionHost host) => _host = host;

    /// <summary>The dialogue line on screen, or null (M8-05).</summary>
    public MissionDialogue? CurrentDialogue => _dialogue.Current;

    /// <summary>
    /// Called once per frame with the sim tick: moves the dialogue on, plays the voice of a line that just started when its file exists, and
    /// returns the cell the camera should centre on, when the script's camera hint is new (M8-05). Null otherwise.
    /// </summary>
    public Rts.Sim.Core.CellCoord? Advance(int tick, IAudio audio)
    {
        _tick = tick;
        _dialogue.Advance(_host.Dialogue, tick);
        if (_dialogue.Started && _dialogue.Current is { } line && MissionDialoguePresenter.VoiceToPlay(line, audio.HasVoice) is { } voice)
            audio.PlayVoice(voice);
        return _cameraCue.Take(_host.CameraHint, tick);
    }

    /// <summary>The dialogue line as HUD text: "SPEAKER: text", wrapped (empty when no line is up).</summary>
    public List<string> DialogueLines()
    {
        MissionDialogue? line = _dialogue.Current;
        return line is null ? new List<string>() : Wrap(line.Speaker.ToUpperInvariant() + ": " + line.Text, WrapChars);
    }

    /// <summary>The objectives panel, so clicks on it never reach the map.</summary>
    public RectRegion ObjectivesRegion { get; } = new(Rectangle.Empty);

    /// <summary>Lines of the objectives panel (<see cref="MissionObjectiveLines"/>).</summary>
    public List<string> ObjectiveLines() => MissionObjectiveLines.Build(_host, _tick);

    /// <summary>The ticker lines for the given sim tick: recent messages, wrapped, newest last.</summary>
    public List<string> TickerLines(int tick)
    {
        var lines = new List<string>();
        foreach (MissionMessage m in _host.Messages)
        {
            if (tick - m.Tick > MessageShowTicks) continue;
            lines.AddRange(Wrap("> " + m.Text, WrapChars));
        }

        return lines.Count > MaxTickerLines ? lines.GetRange(lines.Count - MaxTickerLines, MaxTickerLines) : lines;
    }

    /// <summary>Where the objectives panel goes: below the tick panel of <paramref name="hudLines"/> lines.</summary>
    public Point ObjectivesOrigin(int hudLines, int scale) => new(8, 8 + Hud.HeightOf(hudLines, scale) + GapPx);

    /// <summary>Updates the click-blocking rectangle of the objectives panel.</summary>
    public void UpdateRegion(int hudLines, int scale)
    {
        List<string> lines = ObjectiveLines();
        Point o = ObjectivesOrigin(hudLines, scale);
        ObjectivesRegion.Bounds = new Rectangle(o.X, o.Y, lines.Max(l => BitmapFont.Measure(l, scale)) + 16, Hud.HeightOf(lines.Count, scale));
    }

    /// <summary>
    /// Draws the panels. <paramref name="helpTop"/> is the y of the key help panel; the ticker sits above it and the dialogue line above the ticker.
    /// </summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, int scale, int hudLines, int helpTop, int tick)
    {
        Hud.Panel(sb, pixel, ObjectivesOrigin(hudLines, scale), ObjectiveLines(), scale);
        int bottom = helpTop - GapPx;
        List<string> ticker = TickerLines(tick);
        if (ticker.Count > 0)
        {
            bottom = helpTop - GapPx - Hud.HeightOf(ticker.Count, scale);
            Hud.Panel(sb, pixel, new Point(8, bottom), ticker, scale);
            bottom -= GapPx;
        }

        List<string> dialogue = DialogueLines();
        if (dialogue.Count > 0) Hud.Panel(sb, pixel, new Point(8, bottom - Hud.HeightOf(dialogue.Count, scale)), dialogue, scale);
    }

    /// <summary>Greedy word wrap to at most <paramref name="width"/> characters per line (a longer word gets its own line).</summary>
    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width) { lines.Add(line); line = "  " + word; }
            else line = line.Length == 0 ? word : line + " " + word;
        }

        if (line.Length > 0) lines.Add(line);
        return lines;
    }
}
