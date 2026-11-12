// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Screens.Editor;

/// <summary>
/// The map editor (design 08 §5). Left: tools and options. Centre: the map (WASD / arrows / middle-drag to pan, wheel to zoom, left to
/// paint, right to erase). Right: the validation panel (click an issue to centre the camera on it). Top: New, Open, Save, Undo, Redo,
/// Validate, Play, Back. All editing logic is in <see cref="EditorSession"/>; this class only reads input and draws.
/// </summary>
public sealed class MapEditorScreen : IScreen
{
    private static readonly int[] Zooms = [3, 4, 6, 8, 12, 16, 24, 32];
    private static readonly Color Bg = new(0x0E, 0x10, 0x0C);
    private static readonly Color PanelFill = new(0x16, 0x18, 0x14);
    private static readonly Color Border = new(0x6A, 0x72, 0x60);
    private static readonly Color Text = new(0xE2, 0xE4, 0xD8);
    private static readonly Color Dim = new(0x8A, 0x90, 0x80);
    private static readonly Color BtnFill = new(0x2A, 0x2E, 0x26);
    private static readonly Color BtnHover = new(0x58, 0x6E, 0x50);
    private static readonly Color BtnActive = new(0x38, 0x4A, 0x38);
    private static readonly Color ErrorColour = new(0xF0, 0x70, 0x60);
    private static readonly Color WarnColour = new(0xE8, 0xC8, 0x60);

    private readonly GameServices _s;
    private readonly Action<string> _play;
    private readonly string _userDir;
    private readonly string _dataDir;
    private readonly IReadOnlyList<FeatureType> _featureTypes;
    private readonly Func<MapData, MapMeta?, TerrainTable?, IReadOnlyList<MapIssue>>? _validator;
    private EditorSession _session;
    private ValidationReport _report = ValidationReport.From(Array.Empty<MapIssue>());
    private bool _revalidate;   // a new blank map shows no errors until the first edit (S18 UX-LM-14); Open sets it
    private bool _saveToData;
    private int _zoom = 3;
    private Point _origin;
    private bool _framed;
    private Texture2D? _tex;
    private bool _texDirty = true;
    private TerrainPalette? _palette;
    private bool _stroking;
    private bool _erasingWithRight;
    private int _issueScroll;
    private int _selectedIssue = -1;
    private List<EditorButton> _buttons = new();
    private List<EditorLabel> _labels = new();
    private Point _mouse;
    private bool _confirmQuit;

    /// <summary>Creates the editor on a blank 64 x 64 Rust Belt map.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="play">Runs with the saved map's path when the player picks "Play this map"; null shows a notice instead.</param>
    /// <param name="userDir">User maps folder (tests override); null = <see cref="MapFolders.UserDir"/>.</param>
    /// <param name="validator">Validator override (tests); null = <see cref="MapValidator.Validate"/>.</param>
    public MapEditorScreen(GameServices services, Action<string>? play = null, string? userDir = null,
        Func<MapData, MapMeta?, TerrainTable?, IReadOnlyList<MapIssue>>? validator = null)
    {
        _s = services;
        _userDir = userDir ?? MapFolders.UserDir;
        _dataDir = MapFolders.DataDir(services.RulesDir);
        _featureTypes = FeatureType.FromRules(services.RulesDir);
        _validator = validator;
        _play = play ?? DefaultPlay;
        _session = new EditorSession(NewMapScreen.Build(0, 64), _featureTypes);
    }

    /// <summary>The validation panel's current report (for tests).</summary>
    public ValidationReport Report => _report;

    /// <summary>The editing session (for tests).</summary>
    public EditorSession Session => _session;

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter() { }

    /// <inheritdoc />
    public void Exit() { _tex?.Dispose(); _tex = null; }

    // ---- update ------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        var scale = _s.UiScale;
        EditorLayout layout = EditorLayout.Compute(_s.Viewport.Width, _s.Viewport.Height, scale);
        _mouse = input.MousePosition;
        if (!_framed) { Frame(layout); _framed = true; }

        BuildWidgets(layout, scale);
        HandleKeys(input, layout, (float)t.ElapsedGameTime.TotalSeconds);
        HandleButtons(input, layout);
        HandleIssues(input, layout, scale);
        HandleMap(input, layout);

        if (_revalidate && !_stroking) { _report = ValidationReport.Run(_session.Document, null, _validator); _revalidate = false; }
        input.ConsumeKeyboard();
    }

    private void Frame(EditorLayout layout)
    {
        _zoom = 3;
        EditorDocument d = _session.Document;
        while (_zoom < Zooms.Length - 1 && Zooms[_zoom + 1] * d.Width <= layout.MapView.Width && Zooms[_zoom + 1] * d.Height <= layout.MapView.Height) _zoom++;
        _origin = EditorLayout.OriginToCentre(layout.MapView, d.Width / 2, d.Height / 2, Zooms[_zoom]);
    }

    private void HandleKeys(InputFrame input, EditorLayout layout, float dt)
    {
        bool ctrl = input.IsDown(Keys.LeftControl) || input.IsDown(Keys.RightControl);
        bool shift = input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift);
        if (ctrl && input.Pressed(Keys.Z)) { if (shift) Redo(); else Undo(); }
        else if (ctrl && input.Pressed(Keys.Y)) Redo();
        else if (ctrl && input.Pressed(Keys.S)) Save(false);
        else if (!ctrl)
        {
            Keys[] toolKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7];
            for (int i = 0; i < toolKeys.Length; i++) if (input.Pressed(toolKeys[i])) _session.Tool = (EditorTool)i;
            if (input.Pressed(Keys.OemOpenBrackets)) _session.BrushSize = BrushShape.Sizes[Math.Max(0, Array.IndexOf(BrushShape.Sizes, _session.BrushSize) - 1)];
            if (input.Pressed(Keys.OemCloseBrackets)) _session.BrushSize = BrushShape.Sizes[Math.Min(BrushShape.Sizes.Length - 1, Array.IndexOf(BrushShape.Sizes, _session.BrushSize) + 1)];
            if (input.Pressed(Keys.F5)) _revalidate = true;
        }

        int pan = (int)(600 * dt);
        if (!ctrl)
        {
            if (input.IsDown(Keys.A) || input.IsDown(Keys.Left)) _origin.X += pan;
            if (input.IsDown(Keys.D) || input.IsDown(Keys.Right)) _origin.X -= pan;
            if (input.IsDown(Keys.W) || input.IsDown(Keys.Up)) _origin.Y += pan;
            if (input.IsDown(Keys.S) || input.IsDown(Keys.Down)) _origin.Y -= pan;
        }

        if (input.Pressed(Keys.Escape))
        {
            if (!_session.Dirty || _confirmQuit) _s.Screens.Pop();
            else { _confirmQuit = true; Say("Unsaved changes: press Esc again to leave"); }
        }
    }

    private void Say(string text) => _statusOverride = text;
    private string _statusOverride = "";

    private void HandleButtons(InputFrame input, EditorLayout layout)
    {
        if (!input.LeftPressed) return;
        foreach (EditorButton b in _buttons)
        {
            if (!b.Rect.Contains(_mouse)) continue;
            if (b.Enabled) { b.Click(); _confirmQuit = false; }
            input.ConsumeLeft();
            return;
        }
        if (layout.TopBar.Contains(_mouse) || layout.Tools.Contains(_mouse)) input.ConsumeLeft();
    }

    private void HandleIssues(InputFrame input, EditorLayout layout, UiUnit scale)
    {
        if (!layout.Issues.Contains(_mouse)) return;
        int rowH = EditorLayout.RowHeight(scale);
        int firstY = layout.Issues.Y + EditorLayout.HeaderHeight(scale);
        int visible = Math.Max(1, (layout.Issues.Bottom - firstY) / rowH);
        if (input.WheelDelta != 0) { _issueScroll = Math.Clamp(_issueScroll - Math.Sign(input.WheelDelta), 0, Math.Max(0, _report.Rows.Count - visible)); input.ConsumeWheel(); }
        if (input.LeftPressed)
        {
            int row = (_mouse.Y - firstY) / rowH + _issueScroll;
            if (_mouse.Y >= firstY && row >= 0 && row < _report.Rows.Count)
            {
                _selectedIssue = row;
                if (_report.Rows[row].Cell is { } c) CentreOn(c.X, c.Y, layout);
            }
            input.ConsumeLeft();
        }
    }

    private void CentreOn(int x, int y, EditorLayout layout) => _origin = EditorLayout.OriginToCentre(layout.MapView, x, y, Zooms[_zoom]);

    private void HandleMap(InputFrame input, EditorLayout layout)
    {
        bool over = layout.MapView.Contains(_mouse);
        int cell = Zooms[_zoom];
        if (over && input.WheelDelta != 0)
        {
            var before = EditorLayout.CellAt(_mouse, _origin, cell);
            _zoom = Math.Clamp(_zoom + Math.Sign(input.WheelDelta), 0, Zooms.Length - 1);
            cell = Zooms[_zoom];
            _origin = new Point(_mouse.X - before.X * cell - cell / 2, _mouse.Y - before.Y * cell - cell / 2);
            input.ConsumeWheel();
        }
        if (over && input.MiddleDragging) { _origin += input.MouseDelta; input.ConsumeMiddle(); }

        var (cx, cy) = EditorLayout.CellAt(_mouse, _origin, cell);
        if (_stroking)
        {
            bool held = _erasingWithRight ? input.RightDown : input.LeftDown;
            if (held && over) _session.ApplyAt(cx, cy);
            if (!held)
            {
                _stroking = false;
                if (_erasingWithRight) _session.Tool = _toolBeforeErase;
                if (_session.EndStroke()) { _revalidate = true; _texDirty = true; }
                else _texDirty = true;
            }
            else _texDirty = true;
            return;
        }

        if (!over) return;
        if (input.LeftPressed) BeginStroke(false, cx, cy, input);
        else if (input.RightPressed) BeginStroke(true, cx, cy, input);
    }

    private EditorTool _toolBeforeErase;

    private void BeginStroke(bool erase, int cx, int cy, InputFrame input)
    {
        _erasingWithRight = erase;
        _toolBeforeErase = _session.Tool;
        if (erase) _session.Tool = EditorTool.Eraser;
        _stroking = true;
        _confirmQuit = false;
        _statusOverride = "";
        _session.BeginStroke();
        _session.ApplyAt(cx, cy);
        if (erase) input.ConsumeRight(); else input.ConsumeLeft();
    }

    // ---- widgets -----------------------------------------------------------------------------------------------

    private void BuildWidgets(EditorLayout layout, UiUnit scale)
    {
        var top = new FlowLayout(layout.TopBar, scale);
        top.Add("New", false, NewMap);
        top.Add("Open", false, OpenMap);
        top.Add("Save", false, () => Save(false));
        top.Add(_saveToData ? "to: data" : "to: user", false, () => _saveToData = !_saveToData);
        top.Add("Name", false, RenameMap);
        top.Add("Undo", false, Undo, _session.History.UndoCount > 0);
        top.Add("Redo", false, Redo, _session.History.RedoCount > 0);
        top.Add("Validate", false, () => _revalidate = true);
        top.Add("Play this map", false, () => Save(true));
        top.Add("Back", false, () => { if (!_session.Dirty || _confirmQuit) _s.Screens.Pop(); else { _confirmQuit = true; Say("Unsaved changes: click Back again to leave"); } });

        var tools = new FlowLayout(layout.Tools, scale);
        string[] toolNames = ["Land", "Height", "Resource", "Feature", "Start", "Eraser", "Fill"];
        tools.Label("Tool (1-7)");
        for (int i = 0; i < toolNames.Length; i++)
        {
            var tool = (EditorTool)i;
            tools.Add(toolNames[i], _session.Tool == tool, () => _session.Tool = tool);
        }
        tools.NewLine();
        switch (_session.Tool)
        {
            case EditorTool.Land:
            case EditorTool.Fill:
                tools.Label("Land type");
                foreach (LandType l in Enum.GetValues<LandType>()) { LandType lt = l; tools.Add(l.ToString(), _session.Land == lt, () => _session.Land = lt); }
                break;
            case EditorTool.Height:
                tools.Label("Height");
                for (int h = 0; h <= 3; h++) { int hv = h; tools.Add(h.ToString(), _session.HeightLevel == hv, () => _session.HeightLevel = hv); }
                break;
            case EditorTool.Resource:
                tools.Label("Resource");
                tools.Add("Ashfall", _session.Resource == OverlayKind.Ashfall, () => _session.Resource = OverlayKind.Ashfall);
                tools.Add("Pitchblende", _session.Resource == OverlayKind.Pitchblende, () => _session.Resource = OverlayKind.Pitchblende);
                tools.NewLine();
                tools.Add("-", false, () => _session.ResourceDensity--);
                tools.Add($"Density {_session.ResourceDensity}", false, () => { });
                tools.Add("+", false, () => _session.ResourceDensity++);
                break;
            case EditorTool.Feature:
                tools.Label("Feature");
                foreach (FeatureType f in _session.FeatureTypes) { FeatureType ft = f; tools.Add($"{f.Id[4..]} {f.Width}x{f.Height}", _session.Feature == ft, () => _session.Feature = ft); }
                break;
            case EditorTool.Start:
                tools.Label("Start slot");
                for (int n = 1; n <= EditorDocument.MaxStarts; n++) { int sv = n; tools.Add(n.ToString(), _session.StartSlot == sv, () => _session.StartSlot = sv); }
                break;
        }
        tools.NewLine();
        tools.Label("Brush ([ ])");
        foreach (int size in BrushShape.Sizes) { int sz = size; tools.Add(size.ToString(), _session.BrushSize == sz, () => _session.BrushSize = sz); }
        tools.NewLine();
        tools.Label("Symmetry");
        string[] symNames = ["None", "Rot2", "Rot4", "Mirror X", "Mirror Y", "Mirror diag"];
        for (int i = 0; i < symNames.Length; i++)
        {
            var sym = (MapSymmetry)i;
            tools.Add(symNames[i], _session.Document.Symmetry == sym, () => { if (_session.SetSymmetry(sym)) _revalidate = true; else Say(_session.Status); },
                BrushShape.SymmetryFits(sym, _session.Document.Width, _session.Document.Height));
        }

        _buttons = top.Buttons.Concat(tools.Buttons).ToList();
        _labels = tools.Labels;
    }

    // ---- actions -----------------------------------------------------------------------------------------------

    private void Undo() { if (_session.Undo()) { _revalidate = true; _texDirty = true; } }

    private void Redo() { if (_session.Redo()) { _revalidate = true; _texDirty = true; } }

    private void NewMap() => _s.Screens.Push(new NewMapScreen(_s, doc =>
    {
        _session = new EditorSession(doc, _featureTypes);
        _framed = false;
        _texDirty = true;
        _revalidate = false;
        _report = ValidationReport.From(Array.Empty<MapIssue>());
        _selectedIssue = -1;
        _currentPath = null;
        Say("New map " + doc.Name);
    }));

    private void RenameMap() => _s.Screens.Push(new TextPromptScreen(_s, "Map name", _session.Document.Name, name =>
    {
        _session.Document.Name = name;
        _session.MarkChanged();
        Say("Named " + name);
    }));

    private string? _currentPath;

    private void OpenMap()
    {
        IReadOnlyList<MapFileEntry> files = MapFolders.List(_userDir, _dataDir);
        var rows = files.Select(f => new ListRow($"{f.Name}  [{(f.IsUser ? "user" : "data")}]", f.Path, () => Open(f), () => MapPreviewCache.For(_s.RulesDir, new MapChoice(f.Path, null)))).ToList();
        _s.Screens.Push(new ListScreen(_s, "Open map", rows, "No maps found", $"{_userDir}  |  {_dataDir}"));
    }

    private void Open(MapFileEntry entry)
    {
        try
        {
            var (map, meta) = MapFile.LoadWithMeta(entry.Path);
            _s.Screens.Pop();
            _session = new EditorSession(EditorDocument.From(map, meta), _featureTypes);
            _currentPath = entry.Path;
            _saveToData = !entry.IsUser;
            _framed = false;
            _texDirty = true;
            _revalidate = true;
            _selectedIssue = -1;
            Say("Opened " + entry.Name);
        }
        catch (MapFileException e)
        {
            _s.Screens.Push(new NoticeScreen(_s, "Cannot open", e.Message));
        }
    }

    private void Save(bool thenPlay)
    {
        _report = ValidationReport.Run(_session.Document, null, _validator);
        _revalidate = false;
        if (!_report.CanSave) { Say($"Fix {_report.ErrorCount} error(s) before saving"); return; }
        string path = MapFolders.PathFor(_saveToData ? _dataDir : _userDir, _session.Document.Name);
        try
        {
            MapFile.Save(_session.Document.ToMapData(), _session.Document.ToMeta(), path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _s.Screens.Push(new NoticeScreen(_s, "Cannot save", e.Message));
            return;
        }
        _currentPath = path;
        _session.MarkSaved();
        Say("Saved " + path);
        if (thenPlay) _play(path);
    }

    private void DefaultPlay(string path) =>
        _s.Screens.Push(new NoticeScreen(_s, "Play this map", "Saved to " + path + "\nThe skirmish setup lists user maps once the new setup screen lands (M4-11); pick it there."));

    // ---- drawing -----------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        var scale = _s.UiScale;
        EditorLayout layout = EditorLayout.Compute(_s.Viewport.Width, _s.Viewport.Height, scale);
        Texture2D px = _s.Pixel;
        Shapes.Fill(sb, px, new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height), Bg);
        DrawMap(sb, layout, scale);

        foreach (Rectangle r in new[] { layout.TopBar, layout.Tools, layout.Issues, layout.StatusBar })
        {
            Shapes.Fill(sb, px, r, PanelFill);
            Shapes.Outline(sb, px, r, Border);
        }
        foreach (EditorLabel l in _labels) BitmapFont.Draw(sb, px, l.Text, l.Position.ToVector2(), Dim, scale);
        foreach (EditorButton b in _buttons)
        {
            Color fill = !b.Enabled ? BtnFill : b.Active ? BtnActive : b.Rect.Contains(_mouse) ? BtnHover : BtnFill;
            Shapes.Fill(sb, px, b.Rect, fill);
            if (b.Active) Shapes.Outline(sb, px, b.Rect, Border);
            BitmapFont.Draw(sb, px, b.Text, new Vector2(b.Rect.X + scale * 3, b.Rect.Y + scale * 1), b.Enabled ? Text : Dim, scale);
        }
        DrawIssues(sb, layout, scale);

        EditorDocument d = _session.Document;
        string status = _statusOverride.Length > 0 ? _statusOverride : _session.Status;
        var (hx, hy) = EditorLayout.CellAt(_mouse, _origin, Zooms[_zoom]);
        string pos = d.Contains(hx, hy) ? $"({hx}, {hy}) {d.Land[d.IndexOf(hx, hy)]}" : "";
        string line = $"{d.Name}{(_session.Dirty ? "*" : "")}  {d.Width}x{d.Height}  {pos}  {status}";
        line = TextWrap.Ellipsize(line, layout.StatusBar.Width - scale * 4, t => BitmapFont.Measure(t, scale));   // never cut off by the window edge
        BitmapFont.Draw(sb, px, line, new Vector2(layout.StatusBar.X + scale * 2, layout.StatusBar.Y + scale * 1), Text, scale);
    }

    private void DrawIssues(SpriteBatch sb, EditorLayout layout, UiUnit scale)
    {
        Texture2D px = _s.Pixel;
        Rectangle r = layout.Issues;
        int room = r.Width - scale * 4;
        string header = TextWrap.Ellipsize("Validation: " + _report.Summary, room, t => BitmapFont.Measure(t, scale));
        BitmapFont.Draw(sb, px, header, new Vector2(r.X + scale * 2, r.Y + scale * 2), _report.ErrorCount > 0 ? ErrorColour : Text, scale);
        int rowH = EditorLayout.RowHeight(scale), y = r.Y + EditorLayout.HeaderHeight(scale);
        for (int i = _issueScroll; i < _report.Rows.Count && y + rowH <= r.Bottom; i++, y += rowH)
        {
            ValidationRow row = _report.Rows[i];
            var rect = new Rectangle(r.X + 1, y, r.Width - 2, rowH);
            if (i == _selectedIssue) Shapes.Fill(sb, px, rect, BtnActive);
            else if (rect.Contains(_mouse)) Shapes.Fill(sb, px, rect, BtnHover);
            string t = TextWrap.Ellipsize(row.Text, room, m => BitmapFont.Measure(m, scale));
            BitmapFont.Draw(sb, px, t, new Vector2(r.X + scale * 2, y + scale), row.Severity == MapIssueSeverity.Error ? ErrorColour : WarnColour, scale);
        }
    }

    private void DrawMap(SpriteBatch sb, EditorLayout layout, UiUnit scale)
    {
        EditorDocument d = _session.Document;
        int cell = Zooms[_zoom];
        Texture2D px = _s.Pixel;
        EnsureTexture(d);
        if (_tex is not null) sb.Draw(_tex, new Rectangle(_origin.X, _origin.Y, d.Width * cell, d.Height * cell), Color.White);

        if (cell >= 12)
        {
            Color grid = new(0, 0, 0, 40);
            for (int x = 0; x <= d.Width; x++) Shapes.Fill(sb, px, new Rectangle(_origin.X + x * cell, _origin.Y, 1, d.Height * cell), grid);
            for (int y = 0; y <= d.Height; y++) Shapes.Fill(sb, px, new Rectangle(_origin.X, _origin.Y + y * cell, d.Width * cell, 1), grid);
        }

        Color axis = new(255, 255, 255, 90);
        Rectangle mapRect = new(_origin.X, _origin.Y, d.Width * cell, d.Height * cell);
        if (d.Symmetry is MapSymmetry.MirrorX or MapSymmetry.Rot4)
            Shapes.Fill(sb, px, new Rectangle(mapRect.Center.X, mapRect.Y, 1, mapRect.Height), axis);
        if (d.Symmetry is MapSymmetry.MirrorY or MapSymmetry.Rot4)
            Shapes.Fill(sb, px, new Rectangle(mapRect.X, mapRect.Center.Y, mapRect.Width, 1), axis);

        foreach (MapFeature f in d.Features)
        {
            FeatureType? ft = _session.FeatureTypes.FirstOrDefault(v => v.Id == f.Type);
            var fr = new Rectangle(_origin.X + f.Cell.X * cell, _origin.Y + f.Cell.Y * cell, (ft?.Width ?? 1) * cell, (ft?.Height ?? 1) * cell);
            Shapes.Fill(sb, px, fr, new Color(0x90, 0x70, 0x40, 170));
            Shapes.Outline(sb, px, fr, new Color(0xF0, 0xD0, 0x80));
        }
        for (int i = 0; i < d.Starts.Count; i++)
        {
            CellCoord c = d.Starts[i];
            var sr = new Rectangle(_origin.X + c.X * cell - cell, _origin.Y + c.Y * cell - cell, cell * 3, cell * 3);
            Shapes.Outline(sb, px, sr, new Color(0x60, 0xE0, 0x80), 2);
            BitmapFont.Draw(sb, px, (i + 1).ToString(), new Vector2(sr.X + cell, sr.Y + cell), Color.White, Math.Max(1, scale.Whole));
        }
        if (_selectedIssue >= 0 && _selectedIssue < _report.Rows.Count && _report.Rows[_selectedIssue].Cell is { } ic)
            Shapes.Outline(sb, px, new Rectangle(_origin.X + ic.X * cell - cell, _origin.Y + ic.Y * cell - cell, cell * 3, cell * 3), ErrorColour, 2);

        if (layout.MapView.Contains(_mouse))
        {
            var (hx, hy) = EditorLayout.CellAt(_mouse, _origin, cell);
            if (d.Contains(hx, hy))
            {
                Color hover = new(255, 255, 255, 70);
                if (_session.Tool is EditorTool.Feature)
                    Shapes.Outline(sb, px, new Rectangle(_origin.X + hx * cell, _origin.Y + hy * cell, _session.Feature.Width * cell, _session.Feature.Height * cell), Color.White, 2);
                else if (_session.Tool is EditorTool.Start)
                    Shapes.Outline(sb, px, new Rectangle(_origin.X + (hx - 1) * cell, _origin.Y + (hy - 1) * cell, cell * 3, cell * 3), Color.White, 2);
                else
                    foreach (var (bx, by) in BrushShape.Stroke(_session.Tool == EditorTool.Fill ? MapSymmetry.None : d.Symmetry, d.Width, d.Height, hx, hy, _session.Tool == EditorTool.Fill ? 1 : _session.BrushSize))
                        Shapes.Fill(sb, px, new Rectangle(_origin.X + bx * cell, _origin.Y + by * cell, cell, cell), hover);
            }
        }
    }

    private void EnsureTexture(EditorDocument d)
    {
        if (_tex is null || _tex.Width != d.Width || _tex.Height != d.Height)
        {
            _tex?.Dispose();
            _tex = new Texture2D(_s.Graphics, d.Width, d.Height);
            _palette = null;
            _texDirty = true;
        }
        if (!_texDirty) return;
        _texDirty = false;
        if (_palette is null)
        {
            try { _palette = TerrainPalette.ForMap(d.ToMapData(), d.ToMeta()); }
            catch (Exception e) when (e is IOException or InvalidDataException or FormatException) { _palette = null; }
        }
        var data = new Color[d.Width * d.Height];
        for (int i = 0; i < data.Length; i++)
        {
            Color c = LandColour(d.Land[i]);
            int lift = d.HeightLevel[i] * 14;
            c = new Color(Math.Min(255, c.R + lift), Math.Min(255, c.G + lift), Math.Min(255, c.B + lift));
            if (d.Overlay[i] == OverlayKind.Ashfall) c = Color.Lerp(c, new Color(0xE0, 0xC0, 0x40), 0.35f + d.Density[i] * 0.04f);
            else if (d.Overlay[i] == OverlayKind.Pitchblende) c = Color.Lerp(c, new Color(0x50, 0xB0, 0xE0), 0.35f + d.Density[i] * 0.04f);
            else if (d.Overlay[i] == OverlayKind.Wall) c = new Color(0x90, 0x90, 0x90);
            data[i] = c;
        }
        _tex.SetData(data);
    }

    private Color LandColour(LandType land)
    {
        if (_palette is { } p)
        {
            TerrainPalette.Rgb b = p.ShadesFor(land).Base;
            return new Color(b.R, b.G, b.B);
        }
        return land switch
        {
            LandType.Clear => new Color(0x7A, 0x6A, 0x50), LandType.Road => new Color(0x6A, 0x68, 0x64), LandType.Rough => new Color(0x5A, 0x50, 0x3C),
            LandType.Ash => new Color(0x8A, 0x86, 0x80), LandType.Water => new Color(0x28, 0x50, 0x78), LandType.Rock => new Color(0x44, 0x42, 0x40),
            LandType.Cliff => new Color(0x30, 0x2C, 0x28), LandType.Coast => new Color(0xB4, 0xA6, 0x78), _ => new Color(0x48, 0x78, 0x90),
        };
    }
}
