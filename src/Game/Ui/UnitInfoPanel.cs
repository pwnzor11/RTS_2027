// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Veterancy;

namespace Rts.Game.Ui;

/// <summary>
/// The small info panel for exactly one selected own unit (VET-5, design 14 §5): name, health now and max, then the rank and the kill
/// value progress to the next rank. The text is pure (<see cref="Lines"/>, unit-tested); <see cref="Draw"/> uses <see cref="Hud.Panel"/>.
/// </summary>
public static class UnitInfoPanel
{
    /// <summary>
    /// The lines for one own unit. Name and health always; the veterancy lines only while veterancy is on (<paramref name="veterancyOn"/>),
    /// so a game without it shows just name and health.
    /// </summary>
    /// <param name="name">The unit's display name.</param>
    /// <param name="health">Current health.</param>
    /// <param name="maxHealth">Maximum health of the unit's type.</param>
    /// <param name="veterancyOn">Whether the [veterancy] rule is on.</param>
    /// <param name="rank">Rank 0..3.</param>
    /// <param name="kill">Kill value.</param>
    /// <param name="nextThreshold">Kill value the next rank needs (0 at the top rank or for a unit that cannot rank).</param>
    public static IReadOnlyList<string> Lines(string name, int health, int maxHealth, bool veterancyOn, int rank, int kill, int nextThreshold)
    {
        var lines = new List<string>(4)
        {
            name,
            "Health " + Math.Max(0, health).ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"
                + Math.Max(0, maxHealth).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (veterancyOn) lines.AddRange(VeterancyText.InfoLines(own: true, rank, kill, nextThreshold));
        return lines;
    }

    /// <summary>Draws the panel with its top-left corner at <paramref name="origin"/>.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, Point origin, IReadOnlyList<string> lines, float uiScale) =>
        Hud.Panel(batch, pixel, origin, lines, uiScale);
}
