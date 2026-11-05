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
    private int _placementType = -1;
    private CellCoord _ghostTopLeft;
    private bool _ghostValid;
    private bool _hasGhost;
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

    /// <summary>What the next click on the map does.</summary>
    public SidebarMode Mode { get; private set; }

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
    public void Update(InputFrame input, Camera camera, int windowWidth, int windowHeight)
    {
        _ui = UiScale.FromBackbuffer(windowHeight);
        _hud = _source.Sim.HudFor(_player);
        _hasAircraft = HasAircraft(_hud);
        if (!_hasAircraft && _tab == SidebarTab.Aircraft) SelectTab(SidebarTab.Structures);
        _layout = SidebarLayout.Compute(windowWidth, windowHeight, _ui, SidebarTabs.CountFor(_hasAircraft));
        _mouse = input.MousePosition;
        _tooltip = null;
        if (_shownCredits < 0) _shownCredits = _hud.Cash + _hud.Stored;
        else _shownCredits = SidebarMetrics.CreditStep(_shownCredits, _hud.Cash + _hud.Stored);

        Vector2 topLeft = camera.WorldFromScreen(Point.Zero);
        _cameraTopLeft = topLeft;
        _viewSizeWorld = new Vector2(_layout.MapView.Width, _layout.MapView.Height) / camera.Zoom;

        if (Mode == SidebarMode.Placement && ReadyOf(_placementType) != _placementType) CancelMode();
        BuildVisibleList();

        if (input.ConsumePressed(Keys.Q)) SelectTab(SidebarTab.Structures);
        if (input.ConsumePressed(Keys.W)) SelectTab(SidebarTab.Defences);
        if (input.ConsumePressed(Keys.E)) SelectTab(SidebarTab.Infantry);
        if (input.ConsumePressed(Keys.R)) SelectTab(SidebarTab.Vehicles);
        if (input.ConsumePressed(Keys.T)) SelectTab(SidebarTab.Upgrades);
        if (_hasAircraft && input.ConsumePressed(Keys.Y)) SelectTab(SidebarTab.Aircraft);

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
    public void DrawMapOverlay(SpriteBatch batch, Texture2D pixel, Camera camera, SimSnapshot snapshot)
    {
        if (Mode == SidebarMode.Placement && _hasGhost && _placementType >= 0)
        {
            BuildingType bt = _source.Rules.Buildings[_placementType];
            float left = _ghostTopLeft.X * Camera.CellPx, top = _ghostTopLeft.Y * Camera.CellPx;
            Rectangle r = camera.ScreenRect(left, top, left + bt.FootprintW * Camera.CellPx, top + bt.FootprintH * Camera.CellPx);
            Color c = _ghostValid ? GoodGreen : BadRed;
            Shapes.Fill(batch, pixel, r, Shapes.Alpha(c, 90));
            Shapes.Outline(batch, pixel, r, c, 2);
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

    /// <summary>Draws the panel, the minimap and the tooltip.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot)
    {
        SidebarLayout l = _layout;
        Shapes.Fill(batch, pixel, l.Panel, PanelFill);
        Shapes.Outline(batch, pixel, l.Panel, Border);

        DrawCredits(batch, pixel, l);
        DrawPowerBar(batch, pixel, l);
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
            int slot = l.SlotAt(_mouse);
            if (_tab == SidebarTab.Upgrades)
            {
                if (slot >= 0 && UpgradeAtSlot(slot) is UpgradeEntry entry)
                {
                    _tooltip = UpgradeTooltip(entry);
                    if (left && UpgradeList.LeftClick(_player, entry) is Command research) _source.Host.Issue(research);
                    else if (right && UpgradeList.RightClick(_player, _hud, entry) is Command cancel) _source.Host.Issue(cancel);
                }
            }
            else if (slot >= 0 && EntryAtSlot(slot) is TierEntry entry)
            {
                if (entry.Locked)
                {
                    _tooltip = new List<string> { CameoNames.Display(IdOf(entry.Option)), $"Cost {entry.Option.Cost}", TierGrouping.NeedsText(entry) };
                }
                else
                {
                    BuildOption option = entry.Option;
                    _tooltip = TooltipFor(option);
                    if (left) LeftClickCameo(option);
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

    private void LeftClickCameo(BuildOption option)
    {
        switch (SidebarCommands.LeftClick(_hud, option))
        {
            case CameoClick.Build:
                if (SidebarCommands.Build(_player, option) is Command build) _source.Host.Issue(build);
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
        int max = Math.Max(0, total - _layout.Rows);
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

    private TierEntry? EntryAtSlot(int slot)
    {
        int row = _scrollRow + slot / Math.Max(1, _layout.Columns), col = slot % Math.Max(1, _layout.Columns);
        if (row < 0 || row >= _rows.Count || _rows[row].Heading) return null;
        return col < _rows[row].Cells.Count ? _rows[row].Cells[col] : null;
    }

    private TierRow? HeadingAtSlot(int slot)
    {
        int row = _scrollRow + slot / Math.Max(1, _layout.Columns);
        return slot % Math.Max(1, _layout.Columns) == 0 && row >= 0 && row < _rows.Count && _rows[row].Heading ? _rows[row] : null;
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

    private List<string> TooltipFor(BuildOption o)
    {
        var lines = new List<string> { CameoNames.Display(IdOf(o)), $"Cost {o.Cost}" };
        bool ready = SidebarCommands.LeftClick(_hud, o) == CameoClick.EnterPlacement;
        if (ready) lines.Add("Ready: click to place");
        else if (o.Blocked != QueueBlockReason.None) lines.Add(BlockReasonText.Of(o.Blocked));
        else lines.Add("Left click: build   Right click: cancel");
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

    private void DrawPowerBar(SpriteBatch batch, Texture2D pixel, SidebarLayout l)
    {
        Rectangle r = l.PowerBar;
        if (r.Height <= 2) return;
        Shapes.Fill(batch, pixel, r, new Color(0x0C, 0x0E, 0x0A));
        (int fill, int use, bool low) = SidebarMetrics.PowerBar(_hud.PowerOut, _hud.PowerUse, r.Height - 2);
        Color c = low ? BadRed : GoodGreen;
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
        for (int slot = 0; slot < l.VisibleSlots; slot++)
        {
            Rectangle r = l.Cameo(slot);
            if (HeadingAtSlot(slot) is TierRow heading)
            {
                var span = new Rectangle(r.X, r.Y, l.Grid.Width, r.Height);
                Shapes.Fill(batch, pixel, new Rectangle(span.X, span.Bottom - Math.Max(2, (int)_ui * 2), span.Width, Math.Max(2, (int)_ui * 2)), heading.Locked ? Border : Gold);
                string text = Fit(TierGrouping.HeadingText(heading), span.Width - 4, _ui * 0.85f);
                int th = BitmapFont.LineHeight(_ui * 0.85f, FontWeight.Bold);
                Text(batch, pixel, text, new Vector2(span.X + 2, span.Bottom - th - Math.Max(4, (int)_ui * 4)), heading.Locked ? DimText : Gold, _ui * 0.85f, FontWeight.Bold);
                continue;
            }

            if (EntryAtSlot(slot) is not TierEntry tierEntry)
            {
                if (_rows.Count == 0 || (_scrollRow + slot / Math.Max(1, l.Columns) < _rows.Count && _rows[_scrollRow + slot / Math.Max(1, l.Columns)].Heading)) continue;
                Shapes.Outline(batch, pixel, r, new Color(0x26, 0x2A, 0x22));
                continue;
            }

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
                CentredText(batch, pixel, PlaceholderArt.ShortName(id), inner, TextColour, _ui, FontWeight.Bold);
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
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Bottom - Math.Max(2, (int)_ui * 2), r.Width * q.Items[0].ProgressPermille / 1000, Math.Max(2, (int)_ui * 2)), q.Items[0].Stalled ? Gold : GoodGreen);
                    if (q.Items[0].Stalled)
                        CentredText(batch, pixel, "WAIT", r, Gold, small);
                }
            }

            if (blocked && count == 0) Shapes.Fill(batch, pixel, r, Veil);
            if (tierEntry.Locked) CentredText(batch, pixel, "LOCKED", r, DimText, small, FontWeight.Bold);
            if (ready)
            {
                Shapes.Outline(batch, pixel, r, GoodGreen, 2);
                CentredText(batch, pixel, "READY", r, GoodGreen, _ui * 0.9f, FontWeight.Bold);
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

    private void DrawTooltip(SpriteBatch batch, Texture2D pixel)
    {
        if (_tooltip is null || _tooltip.Count == 0) return;
        float scale = _ui * 0.9f;
        int lh = BitmapFont.LineHeight(scale, FontWeight.Regular);
        int w = _tooltip.Max(line => BitmapFont.Measure(line, scale, FontWeight.Regular)) + 12;
        int h = lh * _tooltip.Count + 8;
        // Left of the cursor (the sidebar is on the right edge), inside the window.
        int x = Math.Max(0, _mouse.X - w - 12), y = Math.Clamp(_mouse.Y - h / 2, 0, Math.Max(0, _layout.Panel.Height - h));
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
            _ => MouseCursor.Arrow,
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
        if (_cursor is null || _cursor == MouseCursor.Arrow) return;
        try
        {
            Mouse.SetCursor(MouseCursor.Arrow);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException)
        {
        }
    }
}
