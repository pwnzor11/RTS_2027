// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Ui.Fonts;
using Rts.Game.Veterancy;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// The classic right-hand sidebar (D-066): credits and power bar, Sell and Repair modes, five tabs (Q W E R, and T for the M3
/// Upgrades tab: a left click researches an available upgrade, a right click cancels a queued one), a grid of cameo
/// buttons for every unlocked type of the tab (greyed with the reason on hover when it cannot be queued), the minimap, and the
/// placement ghost. It only reads the sim (<see cref="PlayerHud"/>, <c>CanPlaceReady</c>) and issues commands through
/// <see cref="SimHost"/>; no rule is duplicated here. While the cursor is over the panel it consumes the mouse, so clicks never
/// reach the map. Left click on a cameo queues (or, for a finished structure, starts placement); right click cancels the
/// front-most queued item of that type.
/// </summary>
public sealed class SidebarPanel : IUiRegion, IDisposable
{
    private static readonly Color PanelFill = new(0x16, 0x18, 0x14, 255);
    private static readonly Color Border = new(0x6A, 0x72, 0x60);
    private static readonly Color TextColour = new(0xE2, 0xE4, 0xD8);
    private static readonly Color DimText = new(0x8A, 0x8C, 0x80);
    private static readonly Color Gold = new(0xD8, 0xC8, 0x8A);
    private static readonly Color ButtonFill = new(0x2A, 0x2E, 0x26);
    private static readonly Color ButtonHover = new(0x44, 0x56, 0x3E);
    private static readonly Color ActiveFill = new(0x58, 0x6E, 0x50);
    private static readonly Color GoodGreen = new(0x5E, 0xD0, 0x5A);
    private static readonly Color BadRed = new(0xE0, 0x4A, 0x32);
    private static readonly Color AmberText = new(0xE8, 0xA8, 0x30);
    private static readonly Color TileFill = new(0x24, 0x2A, 0x22);
    private static readonly Color Veil = new(0, 0, 0, 150);

    private readonly SimSnapshotSource _source;
    private readonly byte _player;
    private readonly Selection _selection;
    private readonly CameoProvider? _cameos;
    private readonly Minimap _minimap;
    private readonly List<BuildOption> _visible = new();
    private IReadOnlyList<TierRow> _rows = Array.Empty<TierRow>();   // M4-09: _visible grouped under tier headings
    private bool _hasAircraft;
    private IReadOnlyList<UpgradeEntry> _upgrades = Array.Empty<UpgradeEntry>();
    private SidebarLayout _layout = SidebarLayout.Compute(1280, 720, 1f);
    private PlayerHud _hud;
    private float _ui = 1f;
    private SidebarTab _tab = SidebarTab.Structures;
    private int _scrollRow;
    private int _shownCredits = -1;
    private bool _minimapDrag;
    private int _shipmentEta = -1, _shipmentPayload, _shipmentDecay;   // M7-14
    private bool _shipmentShown;
    private bool _supplyShown;   // SUP-4: the supply counter row is laid out and drawn
    private int _placementType = -1;
    private CellCoord _ghostTopLeft;
    private bool _ghostValid;
    private bool _hasGhost;
    private CellProblem[] _ghostCells = Array.Empty<CellProblem>();   // M11-10f: why each footprint cell is refused
    private string? _ghostReason;
    private List<string>? _tooltip;
    private Point _mouse;
    private MouseCursor? _cursor;
    private Vector2 _cameraTopLeft;
    private Vector2 _viewSizeWorld;
    private Color _houseColour;

    /// <summary>Creates the sidebar for the local player.</summary>
    /// <param name="source">The sim source (commands go through its host; the sim answers placement questions).</param>
    /// <param name="player">The local player's slot.</param>
    /// <param name="selection">The unit selection (minimap right click orders it).</param>
    /// <param name="cameos">Sprite cameos, or null for placeholders only.</param>
    /// <param name="houseColour">The local player's colour, used to tint cameos.</param>
    public SidebarPanel(SimSnapshotSource source, byte player, Selection selection, CameoProvider? cameos, Color houseColour)
    {
        _source = source;
        _player = player;
        _selection = selection;
        _cameos = cameos;
        _houseColour = houseColour;
        _minimap = new Minimap(source.Map, source.Sim, player);
        _hud = source.Sim.HudFor(player);
    }

    /// <summary>M11-10e: a fully transparent OS cursor, set by the game screen. While it is set and the mouse is over the bare map
    /// (no mode), the OS cursor is hidden and the game screen draws a software cursor instead.</summary>
    public MouseCursor? HiddenCursor { get; set; }

    /// <summary>M11-10g: one-line descriptions by rules id (the archive subtitle), or null.</summary>
    public Func<string, string?>? Blurbs { get; set; }

    /// <summary>True when the software cursor should be drawn: the mouse is over the map and no sidebar mode owns the cursor.</summary>
    public bool WantsSoftwareCursor => HiddenCursor is not null && Mode == SidebarMode.None
        && _layout.MapView.Contains(_mouse) && !_layout.Panel.Contains(_mouse);

    /// <summary>Gives the OS cursor back (the game screen is not on top, for example under the pause menu).</summary>
    public void ShowOsCursor()
    {
        if (_cursor is null || _cursor == MouseCursor.Arrow) return;
        _cursor = null;
        UpdateCursor();
    }

    /// <summary>What the next click on the map does.</summary>
    public SidebarMode Mode { get; private set; }

    /// <summary>True while the left button drags the view on the minimap (the edge scroll then stays off).</summary>
    public bool MinimapDragging => _minimapDrag;

    /// <summary>The selected tab.</summary>
    public SidebarTab Tab => _tab;

    /// <summary>The layout computed by the last <see cref="Update"/>.</summary>
    public SidebarLayout Layout => _layout;

    /// <inheritdoc />
    public bool HitTest(Point screen) => _layout.Panel.Contains(screen);

    /// <summary>Selects a tab (scroll resets).</summary>
    public void SelectTab(SidebarTab tab)
    {
        if (tab == _tab) return;
        _tab = tab;
        _scrollRow = 0;
    }

    /// <summary>Leaves placement, sell and repair mode. Returns true if a mode was active.</summary>
    public bool CancelMode()
    {
        bool was = Mode != SidebarMode.None;
        Mode = SidebarMode.None;
        _placementType = -1;
        _hasGhost = false;
        return was;
    }

    /// <summary>
    /// Handles one frame of input: tab keys, the panel, the minimap, and (in a mode) clicks on the map. Call before the map handles
    /// the mouse; consumed buttons are not seen by it. Esc is left to the caller (use <see cref="CancelMode"/>).
    /// </summary>
    /// <param name="input">This frame's input.</param>
    /// <param name="camera">The camera (the minimap moves it; the ghost reads it).</param>
    /// <param name="windowWidth">Window width in pixels.</param>
    /// <param name="windowHeight">Window height in pixels.</param>
    /// <param name="uiScale">The effective UI scale (<see cref="UiScale.Resolve"/>).</param>
    public void Update(InputFrame input, Camera camera, int windowWidth, int windowHeight, float uiScale = 0f)
    {
        _ui = uiScale > 0f ? uiScale : UiScale.Resolve(windowWidth, windowHeight, UiScale.DefaultPercent).Scale;
        _hud = _source.Sim.HudFor(_player);
        _hasAircraft = HasAircraft(_hud);
        if (!_hasAircraft && _tab == SidebarTab.Aircraft) SelectTab(SidebarTab.Structures);
        _supplyShown = SupplyReadout.Shown(_source.Rules.Supply.Enabled, _hud.Supply);   // SUP-4
        Rts.Sim.View.PlayerView pv = _source.Sim.ViewFor(_player);   // M7-14: shipment timer, own state only
        _shipmentEta = pv.ShipmentEtaTicks; _shipmentPayload = pv.NextShipmentPayload; _shipmentDecay = pv.ShipmentDecayStep;
        _shipmentShown = ShipmentReadout.Shown(_shipmentEta, _shipmentPayload);
        _layout = SidebarLayout.Compute(windowWidth, windowHeight, _ui, SidebarTabs.CountFor(_hasAircraft), _supplyShown, _shipmentShown, _source.Map.Width, _source.Map.Height);
        _mouse = input.MousePosition;
        _tooltip = null;
        if (_shownCredits < 0) _shownCredits = _hud.Cash + _hud.Stored;
        else _shownCredits = SidebarMetrics.CreditStep(_shownCredits, _hud.Cash + _hud.Stored);

        Vector2 topLeft = camera.WorldFromScreen(Point.Zero);
        _cameraTopLeft = topLeft;
        _viewSizeWorld = new Vector2(_layout.MapView.Width, _layout.MapView.Height) / camera.Zoom;

        if (Mode == SidebarMode.Placement && ReadyOf(_placementType) != _placementType) CancelMode();
        BuildVisibleList();

        if (input.ConsumePressed(GameAction.TabStructures)) SelectTab(SidebarTab.Structures);
        if (input.ConsumePressed(GameAction.TabDefences)) SelectTab(SidebarTab.Defences);
        if (input.ConsumePressed(GameAction.TabInfantry)) SelectTab(SidebarTab.Infantry);
        if (input.ConsumePressed(GameAction.TabVehicles)) SelectTab(SidebarTab.Vehicles);
        if (input.ConsumePressed(GameAction.TabUpgrades)) SelectTab(SidebarTab.Upgrades);
        if (_hasAircraft && input.ConsumePressed(GameAction.TabAircraft)) SelectTab(SidebarTab.Aircraft);

        if (_minimapDrag)
        {
            if (input.LeftDown)
            {
                MoveCameraToMinimap(camera, _mouse);
                input.ConsumeLeft();
            }
            else _minimapDrag = false;
        }

        if (_layout.Panel.Contains(_mouse))
        {
            _hasGhost = false;
            UpdatePanel(input, camera);
            input.ConsumeMouse();
        }
        else if (Mode != SidebarMode.None)
        {
            UpdateMapMode(input, camera);
        }

        UpdateCursor();
    }

    /// <summary>Draws the placement ghost and the sell or repair highlight on the map (call before <see cref="Draw"/>).</summary>
    public void DrawMapOverlay(SpriteBatch batch, Texture2D pixel, Camera camera, SimSnapshot snapshot, SceneRenderer? scene = null)
    {
        if (Mode == SidebarMode.Placement && _hasGhost && _placementType >= 0)
        {
            DrawBeachhead(batch, pixel, camera);   // M7-14
            BuildingType bt = _source.Rules.Buildings[_placementType];
            float left = _ghostTopLeft.X * Camera.CellPx, top = _ghostTopLeft.Y * Camera.CellPx;
            Rectangle r = camera.ScreenRect(left, top, left + bt.FootprintW * Camera.CellPx, top + bt.FootprintH * Camera.CellPx);
            DrawBuildRadius(batch, pixel, camera);   // M11-10f
            scene?.DrawBuildingGhost(batch, pixel, _source.Rules, _placementType, _ghostTopLeft, camera, snapshot.Tick, _player);
            Color c = _ghostValid ? GoodGreen : BadRed;
            for (int gy = 0; gy < bt.FootprintH; gy++)
                for (int gx = 0; gx < bt.FootprintW; gx++)
                {
                    bool bad = !_ghostValid && (gy * bt.FootprintW + gx < _ghostCells.Length && _ghostCells[gy * bt.FootprintW + gx] != CellProblem.None
                        || _ghostCells.All(cp => cp == CellProblem.None));   // refused only by the radius, a seal or the beachhead: every cell red
                    float cl = (_ghostTopLeft.X + gx) * Camera.CellPx, ct = (_ghostTopLeft.Y + gy) * Camera.CellPx;
                    Shapes.Fill(batch, pixel, camera.ScreenRect(cl, ct, cl + Camera.CellPx, ct + Camera.CellPx),
                        Shapes.Alpha(bad ? BadRed : GoodGreen, 80));
                }

            Shapes.Outline(batch, pixel, r, c, 2);
            if (_ghostReason is not null)
            {
                int w = BitmapFont.Measure(_ghostReason, _ui, FontWeight.Bold);
                var at = new Vector2(Math.Clamp(r.X, 0, Math.Max(0, _layout.MapView.Right - w - 8)), Math.Max(0, r.Y - BitmapFont.LineHeight(_ui, FontWeight.Bold) - 6));
                Shapes.Fill(batch, pixel, new Rectangle((int)at.X - 3, (int)at.Y - 1, w + 6, BitmapFont.LineHeight(_ui, FontWeight.Bold) + 2), new Color(0, 0, 0, 200));
                Text(batch, pixel, _ghostReason, at, BadRed, _ui, FontWeight.Bold);
            }
        }
        else if ((Mode == SidebarMode.Sell || Mode == SidebarMode.Repair) && !_layout.Panel.Contains(_mouse))
        {
            BuildingView? b = OwnBuildingAt(snapshot, Camera.CellAt(camera.WorldFromScreen(_mouse)));
            if (b is BuildingView hit)
            {
                float left = hit.Cell.X * Camera.CellPx, top = hit.Cell.Y * Camera.CellPx;
                Rectangle r = camera.ScreenRect(left, top, left + hit.FootprintW * Camera.CellPx, top + hit.FootprintH * Camera.CellPx);
                Shapes.Outline(batch, pixel, r, Mode == SidebarMode.Sell ? BadRed : Gold, 3);
            }

            string label = Mode == SidebarMode.Sell ? "SELL" : "REPAIR";
            Text(batch, pixel, label, new Vector2(_mouse.X + 16, _mouse.Y + 16), Mode == SidebarMode.Sell ? BadRed : Gold, _ui, FontWeight.Bold);
        }
    }

    // M11-10f: the sim's verdict, plus a cause for each footprint cell and one line of words. The verdict (_ghostValid) is never overridden.
    private void ExplainGhost(BuildingType bt)
    {
        int w = bt.FootprintW, h = bt.FootprintH;
        if (_ghostCells.Length != w * h) _ghostCells = new CellProblem[w * h];
        Rts.Sim.World.PlacementResult result = _source.Sim.CanPlace(_player, _placementType, _ghostTopLeft);
        var mask = bt.WaterBuilding && bt.WaterMask.Count == w * h;
        var map = _source.Sim.Map;
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new CellCoord(_ghostTopLeft.X + dx, _ghostTopLeft.Y + dy);
                if (!_source.Map.Contains(c)) { _ghostCells[dy * w + dx] = CellProblem.Terrain; continue; }
                LandType land = map.LandAt(c);
                bool wantWater = bt.WaterBuilding && (!mask || bt.WaterMask[dy * w + dx]);
                bool occupied = map.OverlayAt(c) != OverlayKind.None || !map.BuildingAt(c).IsNone || (wantWater && !map.VehicleAt(c).IsNone);
                _ghostCells[dy * w + dx] = PlacementCheck.Cell(wantWater, land is LandType.Water or LandType.Shallow,
                    map.IsBuildable(c, _source.Rules.Terrain), occupied, UnitOnCell(c));
            }

        _ghostReason = _ghostValid ? null : PlacementCheck.Reason(result, _ghostValid, PlacementCheck.Worst(_ghostCells));
    }

    private bool UnitOnCell(CellCoord c)
    {
        foreach (UnitView u in _source.Current.Units)
        {
            if (u.Position.ToCell() != c) continue;
            if (u.Kind == UnitKind.Vehicle && (uint)u.TypeIndex < (uint)_source.Rules.Vehicles.Count
                && _source.Rules.Vehicles[u.TypeIndex].Locomotion == Locomotion.Air) continue;
            return true;
        }

        return false;
    }

    // M11-10f: while placing, outline where building is allowed: each own building's footprint grown by the build radius.
    private void DrawBuildRadius(SpriteBatch batch, Texture2D pixel, Camera camera)
    {
        int radius = _source.Rules.Economy.BuildRadius;
        foreach (BuildingView b in _source.Current.Buildings)
        {
            if (b.Ghost || b.Player != _player) continue;
            if ((uint)b.TypeIndex < (uint)_source.Rules.Buildings.Count && (_source.Rules.Buildings[b.TypeIndex].Wall || _source.Rules.Buildings[b.TypeIndex].Gate)) continue;
            float l = (b.Cell.X - radius) * Camera.CellPx, t = (b.Cell.Y - radius) * Camera.CellPx;
            Rectangle r = camera.ScreenRect(l, t, l + (b.FootprintW + 2 * radius) * Camera.CellPx, t + (b.FootprintH + 2 * radius) * Camera.CellPx);
            Shapes.DashedOutline(batch, pixel, r, Shapes.Alpha(GoodGreen, 150), 2, 10, 8);
        }
    }

    // M7-14: while placing, tint the visible cells of the player's beachhead zone (Expedition only; the mask is empty for others).
    private void DrawBeachhead(SpriteBatch batch, Texture2D pixel, Camera camera)
    {
        Rts.Sim.View.PlayerView pv = _source.Sim.ViewFor(_player);
        if (pv.BeachheadMask.IsEmpty) return;
        int mw = _source.Map.Width, mh = _source.Map.Height;
        Vector2 a = camera.WorldFromScreen(new Point(_layout.MapView.Left, _layout.MapView.Top));
        Vector2 b = camera.WorldFromScreen(new Point(_layout.MapView.Right, _layout.MapView.Bottom));
        int x0 = Math.Max(0, (int)MathF.Floor(a.X / Camera.CellPx)), y0 = Math.Max(0, (int)MathF.Floor(a.Y / Camera.CellPx));
        int x1 = Math.Min(mw - 1, (int)MathF.Floor(b.X / Camera.CellPx)), y1 = Math.Min(mh - 1, (int)MathF.Floor(b.Y / Camera.CellPx));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!BeachheadOverlay.Contains(pv.BeachheadMask, mw, x, y)) continue;
                Shapes.Fill(batch, pixel, camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx),
                    Shapes.Alpha(BeachheadOverlay.Tint, BeachheadOverlay.TintAlpha));
            }
    }

    /// <summary>Draws the panel, the minimap and the tooltip.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot)
    {
        SidebarLayout l = _layout;
        Shapes.Fill(batch, pixel, l.Panel, PanelFill);
        Shapes.Outline(batch, pixel, l.Panel, Border);

        DrawCredits(batch, pixel, l);
        if (_supplyShown) DrawSupply(batch, pixel, l);
        if (_shipmentShown) DrawShipment(batch, pixel, l);
        DrawPowerBar(batch, pixel, l, snapshot.Tick);
        DrawButton(batch, pixel, l.SellButton, "SELL", Mode == SidebarMode.Sell);
        DrawButton(batch, pixel, l.RepairButton, "REPAIR", Mode == SidebarMode.Repair);
        for (int i = 0; i < l.Tabs.Count; i++)
        {
            var tab = (SidebarTab)i;
            string caption = SidebarTabs.KeyLetter(tab) + " " + SidebarTabs.Caption(tab);
            // Five or six tabs share the width: when the key letter does not fit, the bare caption does.
            if (BitmapFont.Measure(caption, _ui * 0.8f, FontWeight.Regular) > l.Tabs[i].Width - 4) caption = SidebarTabs.Caption(tab);
            DrawButton(batch, pixel, l.Tabs[i], caption, tab == _tab, _ui * 0.8f);
        }

        DrawGrid(batch, pixel, l);
        if (LeftBehindText.ShowUnpackHint(_hud, snapshot, _player)) DrawUnpackHint(batch, pixel, l);   // M6-09
        _minimap.Draw(batch, pixel, l.Minimap, snapshot, _cameraTopLeft, _viewSizeWorld);
        DrawTooltip(batch, pixel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _minimap.Dispose();
        ResetCursor();
    }

    // M6-09: a Left Behind player starts with one packed rig and nothing to build; say so in the empty grid.
    private void DrawUnpackHint(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        string text = LeftBehindText.Placeholder(LeftBehindText.UnpackHintKey);
        float scale = _ui * 0.85f;
        int y = l.Grid.Y + (int)(_ui * 6);
        foreach (string line in Rts.Game.Menu.TextWrap.Wrap(text, Math.Max(8, l.Grid.Width - 8), s => BitmapFont.Measure(s, scale, FontWeight.Regular)))
        {
            Text(batch, pixel, line, new Vector2(l.Grid.X + 4, y), Gold, scale, FontWeight.Regular);
            y += BitmapFont.LineHeight(scale, FontWeight.Regular) + 2;
        }
    }

    // ---- input ----------------------------------------------------------------------------------

    private void UpdatePanel(InputFrame input, Camera camera)
    {
        SidebarLayout l = _layout;
        bool left = input.LeftPressed, right = input.RightPressed;

        if (l.Credits.Contains(_mouse)) _tooltip = new List<string> { "Credits", $"Cash {_hud.Cash}   Stored {_hud.Stored}", $"Storage {_hud.StorageCap}" };
        if (_supplyShown && l.SupplyRow.Contains(_mouse)) _tooltip = new List<string> { UiText.Get(UiText.SupplyLabel), $"Used {_hud.Supply.Used}   Cap {_hud.Supply.Cap}" };
        if (l.PowerBar.Contains(_mouse)) _tooltip = new List<string> { "Power", $"Output {_hud.PowerOut}   Use {_hud.PowerUse}", _hud.PowerUse > _hud.PowerOut ? "Low power: production is slower" : "Power is fine" };

        if (l.SellButton.Contains(_mouse))
        {
            _tooltip = new List<string> { "Sell mode", "Click one of your buildings to sell it", "Right click or Esc to leave" };
            if (left) SetMode(Mode == SidebarMode.Sell ? SidebarMode.None : SidebarMode.Sell);
        }
        else if (l.RepairButton.Contains(_mouse))
        {
            _tooltip = new List<string> { "Repair mode", "Click one of your buildings to toggle repair", "Right click or Esc to leave" };
            if (left) SetMode(Mode == SidebarMode.Repair ? SidebarMode.None : SidebarMode.Repair);
        }

        int tabHit = l.TabAt(_mouse);
        if (tabHit >= 0)
        {
            var tab = (SidebarTab)tabHit;
            _tooltip = new List<string> { $"{SidebarTabs.Title(tab)}  ({SidebarTabs.KeyLetter(tab)})" };
            if (left) SelectTab(tab);
        }

        if (l.Grid.Contains(_mouse))
        {
            int wheel = input.WheelDelta;
            if (wheel != 0) ScrollGrid(wheel > 0 ? -1 : 1);
            int slot = _tab == SidebarTab.Upgrades ? l.SlotAt(_mouse) : -1;
            TierEntry? tierHit = _tab == SidebarTab.Upgrades ? null : TierEntryAt(_mouse);
            if (_tab == SidebarTab.Upgrades)
            {
                if (slot >= 0 && UpgradeAtSlot(slot) is UpgradeEntry entry)
                {
                    _tooltip = UpgradeTooltip(entry);
                    if (left && UpgradeList.LeftClick(_player, entry) is Command research) _source.Host.Issue(research);
                    else if (right && UpgradeList.RightClick(_player, _hud, entry) is Command cancel) _source.Host.Issue(cancel);
                }
            }
            else if (tierHit is TierEntry entry)
            {
                if (entry.Locked)
                {
                    _tooltip = CameoTooltip.Head(_source.Rules, entry.Option.Category, entry.Option.TypeIndex, entry.Option.Cost, Blurbs);
                    _tooltip.Add(TierGrouping.NeedsText(entry));
                }
                else
                {
                    BuildOption option = entry.Option;
                    _tooltip = TooltipFor(option);
                    if (left) LeftClickCameo(option, input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift));
                    else if (right) RightClickCameo(option);
                }
            }
        }

        MinimapMath m = MinimapMath.Fit(l.Minimap, _source.Map.Width, _source.Map.Height);
        if (m.Contains(_mouse))
        {
            if (left)
            {
                _minimapDrag = true;
                MoveCameraToMinimap(camera, _mouse);
            }
            else if (right) OrderSelectionTo(m.OrderCell(_mouse));
        }
        else if (right && Mode != SidebarMode.None && l.Grid.Contains(_mouse) == false)
        {
            CancelMode();
        }
    }

    private void UpdateMapMode(InputFrame input, Camera camera)
    {
        CellCoord cell = Camera.CellAt(camera.WorldFromScreen(_mouse));
        if (Mode == SidebarMode.Placement && _placementType >= 0)
        {
            BuildingType bt = _source.Rules.Buildings[_placementType];
            _ghostTopLeft = SidebarCommands.TopLeftFor(cell, bt.FootprintW, bt.FootprintH);
            _ghostValid = _source.Sim.CanPlaceReady(_player, _placementType, _ghostTopLeft);
            ExplainGhost(bt);
            // M7-07: CanPlace also refuses a footprint outside an Expedition player's beachhead zone, so the ghost turns red there.
            _hasGhost = true;
            if (input.LeftPressed)
            {
                if (_ghostValid)
                {
                    _source.Host.Issue(SidebarCommands.Place(_player, _placementType, _ghostTopLeft));
                    CancelMode();
                }

                input.ConsumeLeft();
            }
        }
        else if (input.LeftPressed)
        {
            if (OwnBuildingAt(_source.Current, cell) is BuildingView hit)
            {
                _source.Host.Issue(Mode == SidebarMode.Sell
                    ? SidebarCommands.Sell(_player, hit.Id)
                    : SidebarCommands.Repair(_player, hit.Id));
            }

            input.ConsumeLeft();
        }

        if (input.RightPressed)
        {
            CancelMode();
            input.ConsumeRight();
        }
    }

    private void LeftClickCameo(BuildOption option, bool shift)
    {
        switch (SidebarCommands.LeftClick(_hud, option))
        {
            case CameoClick.Build:
                // M11-10g: Shift+click queues five units; the sim refuses what does not fit (full queue, no cash, supply cap).
                if (SidebarCommands.Build(_player, option) is Command build)
                    for (int n = SidebarCommands.QueueCount(option.Category, shift); n > 0; n--) _source.Host.Issue(build);
                break;
            case CameoClick.EnterPlacement:
                Mode = SidebarMode.Placement;
                _placementType = option.TypeIndex;
                break;
        }
    }

    private void RightClickCameo(BuildOption option)
    {
        if (Mode == SidebarMode.Placement) CancelMode();
        if (SidebarCommands.Cancel(_player, _hud, option) is Command cancel) _source.Host.Issue(cancel);
    }

    private void SetMode(SidebarMode mode)
    {
        CancelMode();
        Mode = mode;
    }

    private void ScrollGrid(int rows)
    {
        int total = TotalRows;
        int max = _tab == SidebarTab.Upgrades
            ? Math.Max(0, total - _layout.Rows)
            : TierGrouping.MaxFirstRow(_rows, _layout.Grid.Height, _layout.CameoSize.Y, HeadingHeight(), _layout.Gap);
        _scrollRow = Math.Clamp(_scrollRow + rows, 0, max);
    }

    private void MoveCameraToMinimap(Camera camera, Point mouse)
    {
        MinimapMath m = MinimapMath.Fit(_layout.Minimap, _source.Map.Width, _source.Map.Height);
        Vector2 target = m.CameraTopLeftFor(mouse, _viewSizeWorld);
        camera.PanScreen((target - camera.WorldFromScreen(Point.Zero)) * camera.Zoom);
    }

    private void OrderSelectionTo(CellCoord cell)
    {
        foreach (UnitKey unit in _selection.Units.ToList()) _source.Order(CommandKind.Move, unit, cell);
    }

    // ---- data -----------------------------------------------------------------------------------

    private int ItemCount => _tab == SidebarTab.Upgrades ? _upgrades.Count : _visible.Count;

    private static bool HasAircraft(PlayerHud hud)
    {
        foreach (BuildOption o in hud.Options)
            if (o.Category == ProductionCategory.Aircraft) return true;
        return false;
    }

    // Grid rows: upgrades are a flat grid; every other tab is tier-grouped rows (headings take a whole row).
    private int TotalRows => _tab == SidebarTab.Upgrades
        ? (_upgrades.Count + _layout.Columns - 1) / Math.Max(1, _layout.Columns)
        : _rows.Count;

    private UpgradeEntry? UpgradeAtSlot(int slot)
    {
        int index = _scrollRow * _layout.Columns + slot;
        return index >= 0 && index < _upgrades.Count ? _upgrades[index] : null;
    }

    private List<string> UpgradeTooltip(UpgradeEntry e)
    {
        var lines = new List<string> { CameoNames.Display(e.Id), $"Cost {e.Cost}" };
        if ((uint)e.Index < (uint)_source.Rules.Upgrades.Count)
            lines.AddRange(UpgradeEffectText.Lines(_source.Rules, _source.Rules.Upgrades[e.Index]));   // M4-09: what it really does
        lines.Add(e.State switch
        {
            UpgradeState.Owned => "Researched",
            UpgradeState.Researching => "Researching   Right click: cancel",
            _ => "Left click: research",
        });
        return lines;
    }

    private void BuildVisibleList()
    {
        _visible.Clear();
        _upgrades = _tab == SidebarTab.Upgrades ? UpgradeList.Entries(_hud, _source.Rules.Upgrades) : Array.Empty<UpgradeEntry>();
        ProductionCategory category = SidebarTabs.CategoryOf(_tab);
        foreach (BuildOption o in _tab == SidebarTab.Upgrades ? Array.Empty<BuildOption>() : _hud.Options)
        {
            if (o.Category != category || IsHidden(o)) continue;
            _visible.Add(o);
        }

        int tier = _source.Sim.TierOf(_player);
        _rows = _tab == SidebarTab.Upgrades
            ? Array.Empty<TierRow>()
            : TierGrouping.Rows(TierGrouping.Entries(_source.Rules, _hud.Faction, tier, category, _visible), _layout.Columns);

        ScrollGrid(0);
    }

    // The starting Construction Yard has no prerequisites and no cost; it is not something to build (see SkirmishStart).
    private bool IsHidden(BuildOption o) =>
        o.Category == ProductionCategory.Structure && IdOf(o) == SkirmishStart.StartBuildingId;

    private int HeadingHeight() => BitmapFont.LineHeight(_ui * 0.85f, FontWeight.Bold) + Math.Max(6, (int)_ui * 6);

    private IReadOnlyList<TierGrouping.PlacedRow> PlacedRows() =>
        TierGrouping.Place(_rows, _scrollRow, _layout.Grid.Height, _layout.CameoSize.Y, HeadingHeight(), _layout.Gap);

    // The rectangle of a cameo of a placed row (columns left to right).
    private Rectangle TierCell(SidebarLayout l, TierGrouping.PlacedRow p, int col) =>
        new(l.Grid.X + col * (l.CameoSize.X + l.Gap), l.Grid.Y + p.Y, l.CameoSize.X, p.Height);

    /// <summary>The screen rectangle of cameo <paramref name="col"/> in grid row <paramref name="row"/> (heading rows count), or empty when not visible.</summary>
    public Rectangle TierCellRect(int row, int col)
    {
        foreach (TierGrouping.PlacedRow p in PlacedRows())
            if (p.Row == row) return TierCell(_layout, p, col);
        return Rectangle.Empty;
    }

    private TierEntry? TierEntryAt(Point at)
    {
        SidebarLayout l = _layout;
        foreach (TierGrouping.PlacedRow p in PlacedRows())
        {
            TierRow row = _rows[p.Row];
            if (row.Heading) continue;
            for (int c = 0; c < row.Cells.Count; c++)
                if (TierCell(l, p, c).Contains(at)) return row.Cells[c];
        }

        return null;
    }

    private string IdOf(BuildOption o)
    {
        SimRules rules = _source.Rules;
        return o.Category switch
        {
            ProductionCategory.Infantry => rules.Infantry[o.TypeIndex].Id,
            ProductionCategory.Vehicle or ProductionCategory.Aircraft => rules.Vehicles[o.TypeIndex].Id,
            _ => rules.Buildings[o.TypeIndex].Id,
        };
    }

    private int ReadyOf(int buildingType)
    {
        if (buildingType < 0 || buildingType >= _source.Rules.Buildings.Count) return -1;
        return SidebarCommands.ReadyType(_hud, _source.Rules.Buildings[buildingType].Category);
    }

    private QueueView? QueueOf(ProductionCategory category)
    {
        foreach (QueueView q in _hud.Queues)
            if (q.Category == category) return q;
        return null;
    }

    private BuildingView? OwnBuildingAt(SimSnapshot snapshot, CellCoord cell)
    {
        foreach (BuildingView b in snapshot.Buildings)
        {
            if (b.Ghost || b.Player != _player) continue;
            if (cell.X >= b.Cell.X && cell.X < b.Cell.X + b.FootprintW && cell.Y >= b.Cell.Y && cell.Y < b.Cell.Y + b.FootprintH) return b;
        }

        return null;
    }

    // SUP-4: the display name of this player's faction supply building, for the cap tooltip.
    private string? SupplyBuildingName() => SupplyBuildingLookup.NameOf(_source.Rules, _hud.Faction);

    // M7-14: a ship in the vehicle lane that no own factory can launch from (the sim queues it and it waits; see ShipyardReach).
    private bool ShipWithoutShipyard(BuildOption o)
    {
        if (o.Category != ProductionCategory.Vehicle) return false;
        Rts.Sim.View.PlayerView pv = _source.Sim.ViewFor(_player);
        var own = new List<Rts.Sim.View.ViewActor>();
        pv.CopyOwn(own);
        return ShipyardReach.NeedsShipyard(_source.Rules, o.TypeIndex, own, c => pv.IsPassable(c, Locomotion.Naval));
    }

    private List<string> TooltipFor(BuildOption o)
    {
        List<string> lines = CameoTooltip.Head(_source.Rules, o.Category, o.TypeIndex, o.Cost, Blurbs);
        bool ready = SidebarCommands.LeftClick(_hud, o) == CameoClick.EnterPlacement;
        if (ready) lines.Add("Ready: click to place");
        else if (o.Blocked != QueueBlockReason.None) lines.Add(BlockReasonText.Of(o.Blocked, SupplyBuildingName()));
        else if (ShipWithoutShipyard(o)) lines.Add(BlockReasonText.NeedsShipyardOnWater);   // M7-14: the ship would wait at the dock
        else lines.Add(CameoTooltip.ClickHint(o.Category));
        return lines;
    }

    // ---- drawing --------------------------------------------------------------------------------

    private void DrawCredits(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        Shapes.Fill(batch, pixel, l.Credits, ButtonFill);
        Shapes.Outline(batch, pixel, l.Credits, Border);
        string text = "$ " + _shownCredits;
        int w = BitmapFont.Measure(text, _ui, FontWeight.Bold), h = BitmapFont.LineHeight(_ui, FontWeight.Bold);
        Text(batch, pixel, text, new Vector2(l.Credits.Right - w - 4 * _ui, l.Credits.Y + (l.Credits.Height - h) / 2f), Gold, _ui, FontWeight.Bold);
    }

    private void DrawSupply(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        Rectangle r = l.SupplyRow;
        if (r.Height <= 0) return;
        Shapes.Fill(batch, pixel, r, ButtonFill);
        Shapes.Outline(batch, pixel, r, Border);
        string text = UiText.Get(UiText.SupplyLabel) + " " + SupplyReadout.Text(_hud.Supply);
        Color colour = SupplyReadout.ToneOf(_hud.Supply.Used, _hud.Supply.Cap) switch
        {
            SupplyTone.Red => BadRed,
            SupplyTone.Amber => AmberText,
            _ => TextColour,
        };
        int w = BitmapFont.Measure(text, _ui, FontWeight.Regular), h = BitmapFont.LineHeight(_ui, FontWeight.Regular);
        Text(batch, pixel, text, new Vector2(r.Right - w - 4 * _ui, r.Y + (r.Height - h) / 2f), colour, _ui, FontWeight.Regular);
    }

    private void DrawShipment(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        Rectangle r = l.ShipmentRow;
        if (r.Height <= 0) return;
        Shapes.Fill(batch, pixel, r, ButtonFill);
        Shapes.Outline(batch, pixel, r, Border);
        string text = ShipmentReadout.Text(_shipmentEta, _shipmentPayload);
        Color colour = ShipmentReadout.Decayed(_shipmentDecay) ? AmberText : TextColour;
        int w = BitmapFont.Measure(text, _ui, FontWeight.Regular), h = BitmapFont.LineHeight(_ui, FontWeight.Regular);
        Text(batch, pixel, text, new Vector2(r.Right - w - 4 * _ui, r.Y + (r.Height - h) / 2f), colour, _ui, FontWeight.Regular);
    }

    // SUP-4: the lane head is paused because it does not fit under the cap (the sim pauses the lane without a Stalled flag).
    private bool SupplyWaits(ProductionCategory category, int typeIndex) =>
        SupplyReadout.HeadWaitsForSupply(_source.Rules.Supply.Enabled, _hud.Supply.Used, _hud.Supply.Cap,
            _source.Sim.SupplyOfType(category, typeIndex));

    private void DrawWaitingForSupply(SpriteBatch batch, Texture2D pixel, Rectangle r, float scale)
    {
        string text = UiText.Get(UiText.SupplyWaiting);
        List<string> lines = Rts.Game.Menu.TextWrap.Wrap(text, Math.Max(8, r.Width - 6), s => BitmapFont.Measure(s, scale, FontWeight.Regular));
        int lh = BitmapFont.LineHeight(scale, FontWeight.Regular);
        int y = r.Y + (r.Height - lines.Count * lh) / 2;
        foreach (string line in lines)
        {
            int w = BitmapFont.Measure(line, scale, FontWeight.Regular);
            Text(batch, pixel, line, new Vector2(r.X + (r.Width - w) / 2f, y), Gold, scale, FontWeight.Regular);
            y += lh;
        }
    }

    // H6: "POWER out / use", red while use exceeds output.
    private void DrawPowerReadout(SpriteBatch batch, Texture2D pixel, Rectangle row, bool low)
    {
        if (row.Height <= 2) return;
        float scale = _ui * 0.8f;
        string text = "POWER " + SidebarMetrics.PowerText(_hud.PowerOut, _hud.PowerUse);
        scale = SidebarMetrics.FitScale(sc => BitmapFont.Measure(text, sc, FontWeight.Regular), row.Width - 2, scale);
        int h = BitmapFont.LineHeight(scale, FontWeight.Regular);
        Text(batch, pixel, text, new Vector2(row.X + 1, row.Y + (row.Height - h) / 2f), low ? BadRed : TextColour, scale, FontWeight.Regular);
    }

    private void DrawPowerBar(SpriteBatch batch, Texture2D pixel, SidebarLayout l, int tick)
    {
        Rectangle r = l.PowerBar;
        if (r.Height <= 2) return;
        Shapes.Fill(batch, pixel, r, new Color(0x0C, 0x0E, 0x0A));
        (int fill, int use, bool low) = SidebarMetrics.PowerBar(_hud.PowerOut, _hud.PowerUse, r.Height - 2);
        Color c = low ? BadRed : GoodGreen;
        DrawPowerReadout(batch, pixel, l.PowerRow, low);   // H6
        if (!SidebarMetrics.PowerBarLit(low, tick)) c = new Color(0x3A, 0x10, 0x0E);   // H6: the bar blinks while low
        if (fill > 0) Shapes.Fill(batch, pixel, new Rectangle(r.X + 1, r.Bottom - 1 - fill, r.Width - 2, fill), c);
        int markY = r.Bottom - 1 - use;
        Shapes.Fill(batch, pixel, new Rectangle(r.X, Math.Clamp(markY, r.Y, r.Bottom - 1), r.Width, 2), Color.White);
        Shapes.Outline(batch, pixel, r, Border);
    }

    private void DrawButton(SpriteBatch batch, Texture2D pixel, Rectangle r, string label, bool active, float scale = -1f)
    {
        if (scale <= 0f) scale = _ui * 0.9f;
        bool hover = r.Contains(_mouse);
        Shapes.Fill(batch, pixel, r, active ? ActiveFill : hover ? ButtonHover : ButtonFill);
        Shapes.Outline(batch, pixel, r, active ? Gold : Border);
        string fitted = Fit(label, r.Width - 4, scale);
        int w = BitmapFont.Measure(fitted, scale, FontWeight.Regular), h = BitmapFont.LineHeight(scale, FontWeight.Regular);
        Text(batch, pixel, fitted, new Vector2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f), active ? Color.White : TextColour, scale, FontWeight.Regular);
    }

    private void DrawUpgradeGrid(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        float small = _ui * 0.75f;
        int lh = BitmapFont.LineHeight(small, FontWeight.Regular);
        for (int slot = 0; slot < l.VisibleSlots; slot++)
        {
            Rectangle r = l.Cameo(slot);
            if (UpgradeAtSlot(slot) is not UpgradeEntry e)
            {
                Shapes.Outline(batch, pixel, r, new Color(0x26, 0x2A, 0x22));
                continue;
            }

            Shapes.Fill(batch, pixel, r, TileFill);
            var inner = new Rectangle(r.X + 2, r.Y + 2, Math.Max(1, r.Width - 4), Math.Max(1, r.Height - 4));
            if (_cameos is null || !_cameos.TryDraw(batch, e.Id, inner, _houseColour))
            {
                Shapes.Fill(batch, pixel, inner, new Color(0x32, 0x3A, 0x2E));
                Shapes.Outline(batch, pixel, inner, new Color(0x4A, 0x54, 0x44));
                CentredText(batch, pixel, PlaceholderArt.ShortName(e.Id), inner, TextColour, _ui, FontWeight.Bold);
            }

            string name = Fit(CameoNames.Display(e.Id), r.Width - 4, small);
            Shapes.Fill(batch, pixel, new Rectangle(r.X + 1, r.Bottom - lh - 2, r.Width - 2, lh + 1), new Color(0, 0, 0, 170));
            Text(batch, pixel, name, new Vector2(r.X + 3, r.Bottom - lh - 2), e.State == UpgradeState.Owned ? DimText : TextColour, small, FontWeight.Regular);

            switch (e.State)
            {
                case UpgradeState.Researching:
                    int done = r.Height * e.ProgressPermille / 1000;
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Y, r.Width, r.Height - done), new Color(0, 0, 0, 120));
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Bottom - 2, r.Width * e.ProgressPermille / 1000, 2), GoodGreen);
                    Shapes.Outline(batch, pixel, r, Gold);
                    break;
                case UpgradeState.Owned:
                    Shapes.Fill(batch, pixel, r, Veil);
                    CentredText(batch, pixel, "OWNED", r, GoodGreen, small, FontWeight.Bold);
                    Shapes.Outline(batch, pixel, r, Border);
                    break;
                default:
                    Shapes.Outline(batch, pixel, r, r.Contains(_mouse) ? Gold : Border);
                    break;
            }
        }
    }

    private void DrawGrid(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        if (_tab == SidebarTab.Upgrades)
        {
            DrawUpgradeGrid(batch, pixel, l);
            return;
        }

        QueueView? queue = QueueOf(SidebarTabs.CategoryOf(_tab));
        float small = _ui * 0.75f;
        foreach (TierGrouping.PlacedRow placed in PlacedRows())
        {
            TierRow heading = _rows[placed.Row];
            if (heading.Heading)
            {
                var span = new Rectangle(l.Grid.X, l.Grid.Y + placed.Y, l.Grid.Width, placed.Height);
                Shapes.Fill(batch, pixel, new Rectangle(span.X, span.Bottom - Math.Max(2, (int)_ui * 2), span.Width, Math.Max(2, (int)_ui * 2)), heading.Locked ? Border : Gold);
                string text = Fit(TierGrouping.HeadingText(heading), span.Width - 4, _ui * 0.85f);
                int th = BitmapFont.LineHeight(_ui * 0.85f, FontWeight.Bold);
                Text(batch, pixel, text, new Vector2(span.X + 2, span.Bottom - th - Math.Max(4, (int)_ui * 4)), heading.Locked ? DimText : Gold, _ui * 0.85f, FontWeight.Bold);
                continue;
            }

            for (int col = 0; col < l.Columns; col++)
            {
            Rectangle r = TierCell(l, placed, col);
            if (col >= heading.Cells.Count)
            {
                Shapes.Outline(batch, pixel, r, new Color(0x26, 0x2A, 0x22));
                continue;
            }

            TierEntry tierEntry = heading.Cells[col];
            BuildOption o = tierEntry.Option;
            string id = IdOf(o);
            bool ready = SidebarCommands.LeftClick(_hud, o) == CameoClick.EnterPlacement;
            bool blocked = (o.Blocked != QueueBlockReason.None && !ready) || tierEntry.Locked;
            Shapes.Fill(batch, pixel, r, TileFill);
            var inner = new Rectangle(r.X + 2, r.Y + 2, Math.Max(1, r.Width - 4), Math.Max(1, r.Height - 4));
            if (_cameos is null || !_cameos.TryDraw(batch, id, inner, _houseColour))
            {
                Shapes.Fill(batch, pixel, inner, new Color(0x32, 0x3A, 0x2E));
                Shapes.Outline(batch, pixel, inner, new Color(0x4A, 0x54, 0x44));
                if (!tierEntry.Locked) CentredText(batch, pixel, PlaceholderArt.ShortName(id), inner, TextColour, _ui, FontWeight.Bold);   // P6: a locked cameo shows LOCKED once, not a ghost under it
            }

            // Name strip along the bottom.
            string name = Fit(CameoNames.Display(id), r.Width - 4, small);
            int lh = BitmapFont.LineHeight(small, FontWeight.Regular);
            Shapes.Fill(batch, pixel, new Rectangle(r.X + 1, r.Bottom - lh - 2, r.Width - 2, lh + 1), new Color(0, 0, 0, 170));
            Text(batch, pixel, name, new Vector2(r.X + 3, r.Bottom - lh - 2), blocked ? DimText : TextColour, small, FontWeight.Regular);

            // Progress of the front item of this type: the unfinished part is veiled from the top.
            int count = 0;
            if (queue is QueueView q)
            {
                for (int i = 0; i < q.Items.Count; i++)
                {
                    if (q.Items[i].TypeIndex != o.TypeIndex) continue;
                    count++;
                    if (i != 0) continue;
                    int done = r.Height * q.Items[0].ProgressPermille / 1000;
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Y, r.Width, r.Height - done), new Color(0, 0, 0, 120));
                    bool waitingForSupply = SupplyWaits(q.Category, q.Items[0].TypeIndex);   // SUP-4
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Bottom - Math.Max(2, (int)_ui * 2), r.Width * q.Items[0].ProgressPermille / 1000, Math.Max(2, (int)_ui * 2)), q.Items[0].Stalled || waitingForSupply ? Gold : GoodGreen);
                    if (waitingForSupply) DrawWaitingForSupply(batch, pixel, r, small);
                    else if (q.Items[0].Stalled)
                        CentredText(batch, pixel, "WAIT", r, Gold, small);
                }
            }

            if (blocked && count == 0) Shapes.Fill(batch, pixel, r, Veil);
            if (tierEntry.Locked) FittedCentredText(batch, pixel, "LOCKED", r, DimText, small);   // P6
            if (ready)
            {
                Shapes.Outline(batch, pixel, r, GoodGreen, 2);
                FittedCentredText(batch, pixel, "READY", r, GoodGreen, _ui * 0.9f);   // P6: sized to the cameo
            }
            else Shapes.Outline(batch, pixel, r, r.Contains(_mouse) ? Gold : Border);

            if (count > 0)
            {
                string badge = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int bw = BitmapFont.Measure(badge, small, FontWeight.Bold) + 6;
                var br = new Rectangle(r.Right - bw - 1, r.Y + 1, bw, lh + 2);
                Shapes.Fill(batch, pixel, br, new Color(0, 0, 0, 200));
                Text(batch, pixel, badge, new Vector2(br.X + 3, br.Y + 1), Gold, small, FontWeight.Bold);
            }
            }
        }
    }

    private void DrawTooltip(SpriteBatch batch, Texture2D pixel)
    {
        if (_tooltip is null || _tooltip.Count == 0) return;
        float scale = _ui * 0.9f;
        int lh = BitmapFont.LineHeight(scale, FontWeight.Regular);
        int w = _tooltip.Max(line => BitmapFont.Measure(line, scale, FontWeight.Regular)) + 12;
        int h = lh * _tooltip.Count + 8;
        // Left of the whole sidebar (over the map edge), so it never covers the cameo grid; inside the window.
        int x = Math.Max(0, Math.Min(_mouse.X - w - 12, _layout.Panel.X - w - 4)), y = Math.Clamp(_mouse.Y - h / 2, 0, Math.Max(0, _layout.Panel.Height - h));
        var box = new Rectangle(x, y, w, h);
        Shapes.Fill(batch, pixel, box, new Color(0, 0, 0, 230));
        Shapes.Outline(batch, pixel, box, Border);
        for (int i = 0; i < _tooltip.Count; i++)
            Text(batch, pixel, _tooltip[i], new Vector2(box.X + 6, box.Y + 4 + i * lh), i == 0 ? Gold : TextColour, scale, FontWeight.Regular);
    }

    private static string Fit(string text, int maxWidth, float scale)
    {
        if (BitmapFont.Measure(text, scale, FontWeight.Regular) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string cut = text[..n].TrimEnd();
            if (BitmapFont.Measure(cut, scale, FontWeight.Regular) <= maxWidth) return cut;
        }

        return "";
    }

    private static void Text(SpriteBatch batch, Texture2D pixel, string text, Vector2 at, Color colour, float scale, FontWeight weight) =>
        BitmapFont.Draw(batch, pixel, text, at, colour, scale, weight);

    // P6: bold label drawn once, scaled down until it fits the cameo with 2 px to spare each side.
    private static void FittedCentredText(SpriteBatch batch, Texture2D pixel, string text, Rectangle r, Color colour, float scale)
    {
        float fit = SidebarMetrics.FitScale(sc => BitmapFont.Measure(text, sc, FontWeight.Bold), r.Width - 4, scale);
        CentredText(batch, pixel, text, r, colour, fit, FontWeight.Bold);
    }

    private static void CentredText(SpriteBatch batch, Texture2D pixel, string text, Rectangle r, Color colour, float scale,
        FontWeight weight = FontWeight.Regular)
    {
        int w = BitmapFont.Measure(text, scale, weight), h = BitmapFont.LineHeight(scale, weight);
        BitmapFont.Draw(batch, pixel, text, new Vector2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f), colour, scale, weight);
    }

    // ---- cursor ---------------------------------------------------------------------------------

    private void UpdateCursor()
    {
        MouseCursor wanted = Mode switch
        {
            SidebarMode.Sell => MouseCursor.Crosshair,
            SidebarMode.Repair => MouseCursor.Hand,
            _ => WantsSoftwareCursor ? HiddenCursor! : MouseCursor.Arrow,   // M11-10e
        };
        if (_cursor == wanted) return;
        _cursor = wanted;
        try
        {
            Mouse.SetCursor(wanted);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException)
        {
            // No windowing backend (tests, headless): the label next to the cursor still shows the mode.
        }
    }

    private void ResetCursor()
    {
        if (_cursor is null || _cursor == MouseCursor.Arrow) return;   // (the hidden cursor is a different object, so it is reset too)
        try
        {
            Mouse.SetCursor(MouseCursor.Arrow);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException)
        {
        }
    }
}
