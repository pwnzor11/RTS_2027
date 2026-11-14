// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Fonts;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Ui.Skin;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Card;

/// <summary>
/// The selection card (M11-04, audit section 3 option B): a panel centred along the bottom of the map view, shown only while something
/// is selected. One unit: portrait, name, type, health, rank, stats and order buttons. A group: a grid of small cameos with health
/// bars. A building: name, health, power, production, rally point, Sell and Repair. <see cref="Refresh"/> builds the model and layout
/// each frame, <see cref="HitTest"/> makes the card a UI region (clicks never reach the map), <see cref="Click"/> routes a click and
/// <see cref="Draw"/> paints it. The data, layout and routing are pure and unit-tested; only <see cref="Draw"/> touches graphics.
/// </summary>
public sealed class SelectionCard : IUiRegion
{
    private static readonly Color Fill = new(0x16, 0x18, 0x14, 242);
    private static readonly Color Frame = new(0xD8, 0x8A, 0x3A);
    private static readonly Color DefaultText = new(0xE2, 0xE4, 0xD8);
    private static readonly Color DimText = new(0xA0, 0xA2, 0x94);
    private static readonly Color Gold = new(0xD8, 0xC8, 0x8A);
    private static readonly Color ButtonFill = new(0x2A, 0x2E, 0x26);
    private static readonly Color ButtonHover = new(0x44, 0x56, 0x3E);
    private static readonly Color ActiveFill = new(0x58, 0x6E, 0x50);
    private static readonly Color Border = new(0x6A, 0x72, 0x60);
    private static readonly Color TileFill = new(0x24, 0x2A, 0x22);
    private static readonly Color BarBack = new(0, 0, 0, 220);

    private readonly SimSnapshotSource _source;
    private readonly Selection _selection;
    private readonly CameoProvider? _cameos;
    private readonly Color _house;
    private float _scale = 1f;

    private Color TextColour => Skin?.Text(DefaultText) ?? DefaultText;   // S24

    /// <summary>S24: the local faction's UI skin, or null for the procedural look.</summary>
    public SkinKit? Skin { get; set; }

    /// <summary>Creates the card for the local player's selection.</summary>
    /// <param name="source">Sim source.</param>
    /// <param name="selection">The unit selection.</param>
    /// <param name="cameos">Cameo art, or null (placeholder boxes).</param>
    /// <param name="house">The local player's colour.</param>
    public SelectionCard(SimSnapshotSource source, Selection selection, CameoProvider? cameos, Color house)
    {
        _source = source;
        _selection = selection;
        _cameos = cameos;
        _house = house;
    }

    /// <summary>The control groups, so the group grid can number its cameos (null: no numbers).</summary>
    public ControlGroups? Groups { get; set; }

    /// <summary>True while the first Sell click is armed: the Sell button then reads "Confirm" and is highlighted (D-205).</summary>
    public bool SellArmed { get; set; }

    /// <summary>What the card shows this frame.</summary>
    public CardModel Model { get; private set; } = CardModel.Empty;

    /// <summary>Where its parts are this frame.</summary>
    public CardLayout Layout { get; private set; } = CardLayout.None;

    /// <summary>True while the card is drawn.</summary>
    public bool Visible => Model.Visible;

    /// <inheritdoc />
    public bool HitTest(Point screen) => CardRouting.Over(Layout, screen);

    /// <summary>Rebuilds the model and the layout. Call once per frame, before input.</summary>
    /// <param name="mapView">The map view rectangle.</param>
    /// <param name="ui">The sidebar's UI scale.</param>
    /// <param name="building">The selected own building, or none.</param>
    public void Refresh(Rectangle mapView, float ui, EntityId building)
    {
        Model = CardModelBuilder.Build(_source, _selection, _source.Local, building, Groups);
        _scale = CardLayout.ScaleFor(mapView.Width, ui);
        int lh = BitmapFont.LineHeight(_scale, FontWeight.Regular);
        int width = Math.Max(120, Math.Min((int)MathF.Round(CardLayout.WidthUnits * _scale), mapView.Width - 2 * (int)MathF.Round(8f * _scale)));
        Layout = CardLayout.Compute(mapView, _scale, lh, Model.Kind, Model.Buttons.Count, Model.Cameos.Count, CardLayout.RowsFor(Model, width, _scale));
    }

    /// <summary>Routes a left click (see <see cref="CardRouting.Click"/>).</summary>
    public CardAction Click(Point at, bool shift) => CardRouting.Click(Layout, Model, at, shift);

    /// <summary>Paints the card.</summary>
    /// <param name="batch">Sprite batch (already begun).</param>
    /// <param name="pixel">White pixel.</param>
    /// <param name="mouse">Cursor, for the button hover.</param>
    /// <param name="attackMoveArmed">Highlights the Attack button while attack-move waits for a click.</param>
    public void Draw(SpriteBatch batch, Texture2D pixel, Point mouse, bool attackMoveArmed)
    {
        if (!Visible) return;
        CardLayout l = Layout;
        float s = _scale;
        Shapes.Fill(batch, pixel, l.Card, Fill);
        if (Skin is null || !Skin.TrySlice(batch, "card_frame", l.Card))
            Shapes.Outline(batch, pixel, l.Card, Frame, Math.Max(1, (int)MathF.Round(s)));

        if (Model.Kind == CardKind.Multi) DrawGroup(batch, pixel, l, s, mouse);
        else DrawSingle(batch, pixel, l, s);

        int hover = CardRouting.ButtonAt(l, mouse);
        for (int i = 0; i < l.Buttons.Count && i < Model.Buttons.Count; i++)
        {
            CardButton b = Model.Buttons[i];
            Rectangle r = l.Buttons[i];
            bool sellArmed = b.Kind == CardButtonKind.Sell && SellArmed;
            if (sellArmed) b = b with { Label = UiText.Get(UiText.SellConfirm) };
            bool active = sellArmed || b.Active || b.Kind == CardButtonKind.Order && b.Action == GameAction.AttackMove && attackMoveArmed;
            bool down = hover == i && Microsoft.Xna.Framework.Input.Mouse.GetState().LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            SkinButtonState state = down ? SkinButtonState.Pressed : hover == i ? SkinButtonState.Hover : SkinButtonState.Normal;
            if (Skin is null || !Skin.TryButton(batch, r, state, active: active))
            {
                Shapes.Fill(batch, pixel, r, active ? ActiveFill : hover == i ? ButtonHover : ButtonFill);
                Shapes.Outline(batch, pixel, r, active ? Gold : Border);
            }

            DrawButtonText(batch, pixel, r, b, s);
        }
    }

    private void DrawButtonText(SpriteBatch batch, Texture2D pixel, Rectangle r, CardButton b, float s)
    {
        float fs = s;
        string text = b.Text;
        while (fs > 1f && BitmapFont.Measure(text, fs, FontWeight.Regular) > r.Width - 4) fs = Math.Max(1f, fs - 0.25f);
        if (BitmapFont.Measure(text, fs, FontWeight.Regular) > r.Width - 2) text = b.Label;   // the key text no longer fits: the label alone
        int w = BitmapFont.Measure(text, fs, FontWeight.Regular), h = BitmapFont.LineHeight(fs, FontWeight.Regular);
        BitmapFont.Draw(batch, pixel, text, new Vector2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f), TextColour, fs, FontWeight.Regular);
    }

    private void DrawSingle(SpriteBatch batch, Texture2D pixel, CardLayout l, float s)
    {
        DrawCameo(batch, pixel, l.Portrait, Model.PortraitId);
        Rectangle t = l.Text;
        int y = t.Y;
        void Line(string text, Color colour, FontWeight weight = FontWeight.Regular)
        {
            BitmapFont.Draw(batch, pixel, Clip(text, t.Width, s, weight), new Vector2(t.X, y), colour, s, weight);
            y += l.LineStep;
        }

        Line(Model.Title, Gold, FontWeight.Bold);
        Line(Model.SubLine, DimText);

        // health bar and number
        string hp = CardGrouping.HealthText(Model.Health, Model.MaxHealth);
        int hpW = BitmapFont.Measure(hp, s, FontWeight.Regular);
        int barH = Math.Max(4, (int)MathF.Round(8f * s));
        var bar = new Rectangle(t.X, y + (l.LineStep - barH) / 2, Math.Max(10, t.Width - hpW - 8), barH);
        DrawHealth(batch, pixel, bar, Model.Health, Model.MaxHealth);
        BitmapFont.Draw(batch, pixel, hp, new Vector2(bar.Right + 6, y), TextColour, s, FontWeight.Regular);
        y += l.LineStep;

        if (Model.RankLine is { } rank) Line(rank, TextColour);
        if (Model.Kind == CardKind.Unit)
        {
            foreach (string stat in CardGrouping.WrapStats(Model.Stats.Select(x => x.Text).ToList(), t.Width, 3, x => BitmapFont.Measure(x, s, FontWeight.Regular)))
                Line(stat, TextColour);
        }

        foreach (string extra in Model.Lines) Line(extra, TextColour);
    }

    private void DrawGroup(SpriteBatch batch, Texture2D pixel, CardLayout l, float s, Point mouse)
    {
        BitmapFont.Draw(batch, pixel, Clip(Model.Title, l.Header.Width, s, FontWeight.Bold), new Vector2(l.Header.X, l.Header.Y), Gold, s, FontWeight.Bold);
        for (int i = 0; i < l.Tiles.Count; i++)
        {
            Rectangle r = l.Tiles[i];
            if (i < l.TileCount && i < Model.Cameos.Count)
            {
                CardCameo c = Model.Cameos[i];
                Shapes.Fill(batch, pixel, r, TileFill);
                var inner = new Rectangle(r.X + 2, r.Y + 2, Math.Max(1, r.Width - 4), Math.Max(1, r.Height - 4 - Math.Max(3, r.Height / 8)));
                DrawCameo(batch, pixel, inner, c.RulesId);
                int bh = Math.Max(3, r.Height / 8);
                DrawHealth(batch, pixel, new Rectangle(r.X + 2, r.Bottom - bh - 2, r.Width - 4, bh), c.Health, c.MaxHealth);
                Shapes.Outline(batch, pixel, r, r.Contains(mouse) ? Gold : Border);
                if (c.Group > 0)   // subtle control-group number, top-left corner of the tile
                {
                    float gs = Math.Max(1f, s * 0.7f);
                    BitmapFont.Draw(batch, pixel, c.Group.ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(r.X + 3, r.Y + 2), Gold, gs, FontWeight.Bold);
                }
            }
            else
            {
                Shapes.Fill(batch, pixel, r, ButtonFill);
                Shapes.Outline(batch, pixel, r, Border);
                string more = "+" + l.Overflow.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int w = BitmapFont.Measure(more, s, FontWeight.Bold), h = BitmapFont.LineHeight(s, FontWeight.Bold);
                BitmapFont.Draw(batch, pixel, more, new Vector2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f), Gold, s, FontWeight.Bold);
            }
        }

        BitmapFont.Draw(batch, pixel, Clip("click: select only it   shift-click: remove", l.Hint.Width, s * 0.85f, FontWeight.Regular),
            new Vector2(l.Hint.X, l.Hint.Y), DimText, s * 0.85f, FontWeight.Regular);
    }

    private void DrawCameo(SpriteBatch batch, Texture2D pixel, Rectangle area, string rulesId)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        if (_cameos is not null && _cameos.TryDraw(batch, rulesId, area, _house)) return;
        Shapes.Fill(batch, pixel, area, new Color(0x32, 0x3A, 0x2E));
        Shapes.Outline(batch, pixel, area, new Color(0x4A, 0x54, 0x44));
        string label = PlaceholderArt.ShortName(rulesId);
        float fs = Math.Max(1f, Math.Min(_scale, area.Width / 40f * 1.5f));
        int w = BitmapFont.Measure(label, fs, FontWeight.Bold), h = BitmapFont.LineHeight(fs, FontWeight.Bold);
        BitmapFont.Draw(batch, pixel, label, new Vector2(area.X + (area.Width - w) / 2f, area.Y + (area.Height - h) / 2f), TextColour, fs, FontWeight.Bold);
    }

    private static void DrawHealth(SpriteBatch batch, Texture2D pixel, Rectangle r, int health, int max)
    {
        Shapes.Fill(batch, pixel, r, BarBack);
        int permille = CardGrouping.HealthPermille(health, max);
        Color c = permille > 500 ? new Color(0x3C, 0xC8, 0x50) : permille > 250 ? new Color(0xE8, 0xC0, 0x30) : new Color(0xD0, 0x30, 0x30);
        int fill = (r.Width - 2) * permille / 1000;
        if (fill > 0) Shapes.Fill(batch, pixel, new Rectangle(r.X + 1, r.Y + 1, fill, Math.Max(1, r.Height - 2)), c);
    }

    private static string Clip(string text, int maxWidth, float s, FontWeight weight)
    {
        if (BitmapFont.Measure(text, s, weight) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string cut = text[..n].TrimEnd();
            if (BitmapFont.Measure(cut, s, weight) <= maxWidth) return cut;
        }

        return "";
    }
}
