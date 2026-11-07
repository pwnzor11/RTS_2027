// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui.Card;

/// <summary>Pure click routing for the selection card: which thing under the cursor, and what the click does.</summary>
public static class CardRouting
{
    /// <summary>True when <paramref name="point"/> is on the card (the card is a UI region: clicks there never reach the map).</summary>
    public static bool Over(CardLayout layout, Point point) => layout.Card.Width > 0 && layout.Card.Contains(point);

    /// <summary>
    /// A left click at <paramref name="point"/>. Off the card: <see cref="CardActionKind.None"/>. On a button: its order, Sell or Repair.
    /// On a unit cameo: select only that unit, or with Shift remove it. Anywhere else on the card (including the "+N" tile): swallowed.
    /// </summary>
    public static CardAction Click(CardLayout layout, CardModel model, Point point, bool shift)
    {
        if (!Over(layout, point)) return new CardAction(CardActionKind.None);
        for (int i = 0; i < layout.Buttons.Count && i < model.Buttons.Count; i++)
        {
            if (!layout.Buttons[i].Contains(point)) continue;
            CardButton b = model.Buttons[i];
            return b.Kind switch
            {
                CardButtonKind.Sell => new CardAction(CardActionKind.Sell),
                CardButtonKind.Repair => new CardAction(CardActionKind.Repair),
                _ => new CardAction(CardActionKind.Order, b.Action),
            };
        }

        for (int i = 0; i < layout.TileCount && i < model.Cameos.Count; i++)
        {
            if (!layout.Tiles[i].Contains(point)) continue;
            return new CardAction(shift ? CardActionKind.Remove : CardActionKind.SelectOnly, Unit: model.Cameos[i].Key);
        }

        return new CardAction(CardActionKind.Swallow);
    }

    /// <summary>The index of the button under <paramref name="point"/>, or -1 (hover highlight).</summary>
    public static int ButtonAt(CardLayout layout, Point point)
    {
        for (int i = 0; i < layout.Buttons.Count; i++)
            if (layout.Buttons[i].Contains(point)) return i;
        return -1;
    }
}
