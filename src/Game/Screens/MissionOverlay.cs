// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;
using Rts.Game.Ui;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Mission mode HUD (D-061): the objectives panel under the tick panel at the top left, and the message ticker (Media.Message lines
/// younger than <see cref="MessageShowTicks"/>) above the key help at the bottom left. Reads the <see cref="MissionHost"/> only.
/// </summary>
internal sealed class MissionOverlay
{
    /// <summary>How long a message stays on screen, in ticks (30 seconds).</summary>
    public const int MessageShowTicks = 600;

    private const int WrapChars = 78;
    private const int MaxTickerLines = 6;
    private const int GapPx = 6;

    private readonly MissionHost _host;

    public MissionOverlay(MissionHost host) => _host = host;

    /// <summary>The objectives panel, so clicks on it never reach the map.</summary>
    public RectRegion ObjectivesRegion { get; } = new(Rectangle.Empty);

    /// <summary>Lines of the objectives panel.</summary>
    public List<string> ObjectiveLines()
    {
        var lines = new List<string> { "OBJECTIVES" };
        foreach (MissionObjective o in _host.Objectives)
        {
            string mark = o.Status switch { ObjectiveStatus.Completed => "[x] ", ObjectiveStatus.Failed => "[!] ", _ => "[ ] " };
            lines.Add(mark + (o.Primary ? "" : "(optional) ") + o.Text);
        }

        return lines;
    }

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

    /// <summary>Draws both panels. <paramref name="helpTop"/> is the y of the key help panel; the ticker sits above it.</summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, int scale, int hudLines, int helpTop, int tick)
    {
        Hud.Panel(sb, pixel, ObjectivesOrigin(hudLines, scale), ObjectiveLines(), scale);
        List<string> ticker = TickerLines(tick);
        if (ticker.Count > 0) Hud.Panel(sb, pixel, new Point(8, helpTop - GapPx - Hud.HeightOf(ticker.Count, scale)), ticker, scale);
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
