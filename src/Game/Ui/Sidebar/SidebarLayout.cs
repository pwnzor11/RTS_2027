// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Where everything in the right-hand sidebar goes, from the window size and the UI scale (<see cref="UiScale.FromBackbuffer"/>:
/// 1, 1.5 or 2). Pure arithmetic on integers and one float: no graphics, so it is unit-tested. The panel is about a fifth of the
/// window width (never narrower than 190 or wider than 280 units of UI scale). Top to bottom: credits; the sell and repair
/// buttons; the four tabs; the cameo grid. A vertical power bar runs down the left edge beside them, and the minimap sits at the
/// bottom in the shape of the map (square when unknown), as wide as the panel allows.
/// </summary>
public sealed class SidebarLayout
{
    private const int MinWidthUnits = 190;
    private const int MaxWidthUnits = 280;
    private const int PowerBarUnits = 10;
    private const int RowUnits = 20;
    private const int CreditsUnits = 24;
    private const int SupplyRowUnits = 14;
    private const int MinCameoWidthUnits = 56;
    private const int MaxColumns = 3;

    private SidebarLayout(Rectangle panel, Rectangle mapView, Rectangle credits, Rectangle supplyRow, Rectangle shipmentRow, Rectangle powerRow, Rectangle powerBar, Rectangle sell,
        Rectangle repair, Rectangle[] tabs, Rectangle grid, Rectangle minimap, int columns, int rows, Point cameoSize, int gap)
    {
        Panel = panel; MapView = mapView; Credits = credits; SupplyRow = supplyRow; ShipmentRow = shipmentRow; PowerRow = powerRow; PowerBar = powerBar; SellButton = sell;
        RepairButton = repair; Tabs = tabs; Grid = grid; Minimap = minimap; Columns = columns; Rows = rows; CameoSize = cameoSize; Gap = gap;
    }

    /// <summary>The whole sidebar.</summary>
    public Rectangle Panel { get; }

    /// <summary>The rest of the window: the part the map is shown in.</summary>
    public Rectangle MapView { get; }

    /// <summary>The credits readout.</summary>
    public Rectangle Credits { get; }

    /// <summary>SUP-4: the supply counter row under the credits; empty when the counter is not shown.</summary>
    public Rectangle SupplyRow { get; }

    /// <summary>M7-14: the shipment timer row under the supply row (empty when the player has no shipments).</summary>
    public Rectangle ShipmentRow { get; }

    /// <summary>H6: the power number row ("Output / Use") directly above the power bar and the controls.</summary>
    public Rectangle PowerRow { get; }

    /// <summary>The vertical power bar.</summary>
    public Rectangle PowerBar { get; }

    /// <summary>The Sell mode toggle.</summary>
    public Rectangle SellButton { get; }

    /// <summary>The Repair mode toggle.</summary>
    public Rectangle RepairButton { get; }

    /// <summary>The tabs, in <see cref="SidebarTab"/> order.</summary>
    public IReadOnlyList<Rectangle> Tabs { get; }

    /// <summary>The area the cameo buttons fill.</summary>
    public Rectangle Grid { get; }

    /// <summary>The minimap area (square).</summary>
    public Rectangle Minimap { get; }

    /// <summary>Cameo buttons per row (1..3).</summary>
    public int Columns { get; }

    /// <summary>Rows of cameos that fit in <see cref="Grid"/>.</summary>
    public int Rows { get; }

    /// <summary>Size of one cameo button.</summary>
    public Point CameoSize { get; }

    /// <summary>Gap between neighbouring buttons.</summary>
    public int Gap { get; }

    /// <summary>Cameos visible at once.</summary>
    public int VisibleSlots => Columns * Rows;

    /// <summary>Rectangle of a visible slot (0 = top left, row-major). Empty for a slot outside the grid.</summary>
    public Rectangle Cameo(int slot)
    {
        if (slot < 0 || slot >= VisibleSlots) return Rectangle.Empty;
        int col = slot % Columns, row = slot / Columns;
        return new Rectangle(Grid.X + col * (CameoSize.X + Gap), Grid.Y + row * (CameoSize.Y + Gap), CameoSize.X, CameoSize.Y);
    }

    /// <summary>The visible slot under a point, or -1.</summary>
    public int SlotAt(Point p)
    {
        if (!Grid.Contains(p)) return -1;
        for (int i = 0; i < VisibleSlots; i++)
            if (Cameo(i).Contains(p)) return i;
        return -1;
    }

    /// <summary>The tab under a point, or -1.</summary>
    public int TabAt(Point p)
    {
        for (int i = 0; i < Tabs.Count; i++)
            if (Tabs[i].Contains(p)) return i;
        return -1;
    }

    /// <summary>Width in pixels of the sidebar for a window width and UI scale.</summary>
    public static int WidthFor(int windowWidth, float uiScale)
    {
        int width = Math.Clamp(windowWidth / 5, Px(MinWidthUnits, uiScale), Px(MaxWidthUnits, uiScale));
        return Math.Min(width, windowWidth / 2);
    }

    /// <summary>Computes the layout for a window of <paramref name="windowWidth"/> x <paramref name="windowHeight"/> pixels.</summary>
    /// <param name="windowWidth">Window width in pixels.</param>
    /// <param name="windowHeight">Window height in pixels.</param>
    /// <param name="uiScale">UI scale (1, 1.5 or 2).</param>
    /// <param name="tabCount">Tabs to lay out (M4: 6 while the Aircraft tab is shown).</param>
    /// <param name="supplyRow">SUP-4: reserve a row for the supply counter under the credits (empty rectangle when false).</param>
    /// <param name="shipmentRow">M7-14: reserve a row for the shipment timer under the supply row (empty rectangle when false).</param>
    /// <param name="mapWidth">C6: map width in cells; with <paramref name="mapHeight"/> the minimap takes the map's shape. 0 = square.</param>
    /// <param name="mapHeight">C6: map height in cells.</param>
    /// <param name="textFactor">The Text size factor (<see cref="UiScale.TextFactor"/>): above 1 the rows, buttons and cameos grow so larger text still fits them.</param>
    public static SidebarLayout Compute(int windowWidth, int windowHeight, float uiScale, int tabCount = SidebarTabs.Count,
        bool supplyRow = false, bool shipmentRow = false, int mapWidth = 0, int mapHeight = 0, float textFactor = 1f)
    {
        float tf = Math.Max(1f, textFactor);
        int pad = Math.Max(2, Px(4, uiScale)), gap = Math.Max(1, Px(3, uiScale));
        int width = WidthFor(windowWidth, uiScale);
        int x0 = windowWidth - width;
        var panel = new Rectangle(x0, 0, width, windowHeight);
        var mapView = new Rectangle(0, 0, x0, windowHeight);
        int innerLeft = x0 + pad, innerRight = windowWidth - pad, innerW = Math.Max(0, innerRight - innerLeft);

        int y = pad;
        var credits = new Rectangle(innerLeft, y, innerW, Px(CreditsUnits * tf, uiScale));
        y = credits.Bottom + gap;

        var supply = Rectangle.Empty;
        if (supplyRow)
        {
            supply = new Rectangle(innerLeft, y, innerW, Px(SupplyRowUnits * tf, uiScale));
            y = supply.Bottom + gap;
        }

        var shipment = Rectangle.Empty;
        if (shipmentRow)
        {
            shipment = new Rectangle(innerLeft, y, innerW, Px(SupplyRowUnits * tf, uiScale));
            y = shipment.Bottom + gap;
        }

        var powerRow = new Rectangle(innerLeft, y, innerW, Px(SupplyRowUnits * tf, uiScale));   // H6
        y = powerRow.Bottom + gap;

        // Minimap: at the bottom, no taller than 40% of the window, in the shape of the map (C6; square when the map is unknown).
        int side = Math.Max(0, Math.Min(innerW, (int)(windowHeight * 0.4f)));
        (int mmW, int mmH) = MinimapSize(side, side, mapWidth, mapHeight);
        var minimap = new Rectangle(innerLeft + (innerW - mmW) / 2, windowHeight - pad - mmH, mmW, mmH);

        int powerW = Px(PowerBarUnits, uiScale);
        int contentX = innerLeft + powerW + gap, contentW = Math.Max(0, innerRight - contentX);
        int belowTop = minimap.Y - gap;
        var powerBar = new Rectangle(innerLeft, y, powerW, Math.Max(0, belowTop - y));

        int rowH = Px(RowUnits * tf, uiScale);
        int half = (contentW - gap) / 2;
        var sell = new Rectangle(contentX, y, half, rowH);
        var repair = new Rectangle(contentX + half + gap, y, contentW - half - gap, rowH);
        y += rowH + gap;

        var tabs = new Rectangle[Math.Clamp(tabCount, 1, SidebarTabs.CountWithAircraft)];
        for (int i = 0; i < tabs.Length; i++)
        {
            int left = contentX + contentW * i / tabs.Length, right = contentX + contentW * (i + 1) / tabs.Length;
            tabs[i] = new Rectangle(left, y, right - left, rowH);
        }
        y += rowH + gap;

        int columns = Math.Clamp((contentW + gap) / (Px(MinCameoWidthUnits, uiScale) + gap), 1, MaxColumns);
        int cameoW = Math.Max(1, (contentW - gap * (columns - 1)) / columns), cameoH = Math.Max(Math.Max(1, cameoW * 3 / 4), tf > 1f ? Px(38 * tf, uiScale) : 1);
        int gridH = Math.Max(0, belowTop - y);
        int rows = Math.Max(0, (gridH + gap) / (cameoH + gap));
        var grid = new Rectangle(contentX, y, contentW, gridH);
        return new SidebarLayout(panel, mapView, credits, supply, shipment, powerRow, powerBar, sell, repair, tabs, grid, minimap, columns, rows,
            new Point(cameoW, cameoH), gap);
    }

    /// <summary>
    /// C6/H8: the largest rectangle of the map's aspect ratio inside <paramref name="maxW"/> x <paramref name="maxH"/>. An unknown map
    /// (width or height 0) gives the square the area allows. The space a wide map does not use goes to the build grid.
    /// </summary>
    public static (int W, int H) MinimapSize(int maxW, int maxH, int mapWidth, int mapHeight)
    {
        maxW = Math.Max(0, maxW); maxH = Math.Max(0, maxH);
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            int side = Math.Min(maxW, maxH);
            return (side, side);
        }

        // Compare mapWidth/mapHeight with maxW/maxH by cross-multiplying: the map is wider than the area when mapWidth * maxH > maxW * mapHeight.
        if ((long)mapWidth * maxH >= (long)maxW * mapHeight)
            return (maxW, (int)((long)maxW * mapHeight / mapWidth));
        return ((int)((long)maxH * mapWidth / mapHeight), maxH);
    }

    private static int Px(float units, float uiScale) => (int)MathF.Round(units * uiScale);
}
