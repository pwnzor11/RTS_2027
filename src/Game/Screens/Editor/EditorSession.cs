// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.Stamps;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Screens.Editor;

/// <summary>
/// The editing logic of the map editor, with no drawing: the document, the current tool and brush, the symmetry helper applied live,
/// strokes recorded as undoable steps. A stroke is <see cref="BeginStroke"/>, any number of <see cref="ApplyAt"/>, then
/// <see cref="EndStroke"/>; the whole stroke is one undo step.
/// </summary>
public sealed class EditorSession
{
    private readonly Dictionary<int, int> _strokeIndex = new();   // lookup only, never iterated
    private readonly List<CellEdit> _strokeCells = new();
    private List<CellCoord>? _startsBefore;
    private List<MapFeature>? _featuresBefore;
    private bool _inStroke;
    private string _strokeLabel = "";
    private (int X, int Y)? _lastCell;

    /// <summary>Creates a session on <paramref name="document"/> with the given feature palette (null = <see cref="FeatureType.Defaults"/>).</summary>
    public EditorSession(EditorDocument document, IReadOnlyList<FeatureType>? features = null, int historyCapacity = EditorHistory.DefaultCapacity, StampLibrary? stamps = null)
    {
        StampLib = stamps;
        Document = document;
        FeatureTypes = features is { Count: > 0 } ? features : FeatureType.Defaults;
        History = new EditorHistory(historyCapacity);
        Feature = FeatureTypes[0];
    }

    private StampLibrary? StampLib;

    /// <summary>The stamp library (the shipped one unless a test gave another); empty when the data folder cannot be found.</summary>
    public IReadOnlyList<Stamp> AllStamps
    {
        get
        {
            if (StampLib is null)
            {
                try { StampLib = StampLibrary.Default; }
                catch (Exception e) when (e is InvalidOperationException or IOException or RulesException) { return Array.Empty<Stamp>(); }
            }
            return StampLib.All;
        }
    }

    /// <summary>Stamps that suit the document's biome (a stamp with no biome list suits every map).</summary>
    public IReadOnlyList<Stamp> StampsForBiome()
    {
        string biome = string.IsNullOrEmpty(Document.Biome) ? "rust_belt" : Document.Biome;
        return AllStamps.Where(t => t.Biomes.Count == 0 || t.Biomes.Contains(biome)).ToList();
    }

    /// <summary>Stamp the Stamp tool places; null picks the first stamp that suits the biome.</summary>
    public Stamp? Stamp { get => _stamp ?? StampsForBiome().FirstOrDefault(); set => _stamp = value; }
    private Stamp? _stamp;

    /// <summary>Quarter turns clockwise 0..3 applied after the optional mirror.</summary>
    public int StampRotation { get => _stampRot; set => _stampRot = (value % 4 + 4) % 4; }
    private int _stampRot;

    /// <summary>Mirror the stamp left to right before rotating.</summary>
    public bool StampMirror { get; set; }

    /// <summary>When the map has a symmetry, also stamp every symmetric image (default on).</summary>
    public bool StampSymmetric { get; set; } = true;

    /// <summary>Size in cells of the stamp's footprint as currently rotated.</summary>
    public (int W, int H) StampFootprint => Stamp is { } t ? StampLibrary.FootprintSize(t, StampRotation) : (0, 0);

    /// <summary>The map being edited.</summary>
    public EditorDocument Document { get; }

    /// <summary>The undo / redo stack.</summary>
    public EditorHistory History { get; }

    /// <summary>Features that can be placed.</summary>
    public IReadOnlyList<FeatureType> FeatureTypes { get; }

    /// <summary>Current tool.</summary>
    public EditorTool Tool { get; set; } = EditorTool.Land;

    /// <summary>Land type the Land and Fill tools paint.</summary>
    public LandType Land { get; set; } = LandType.Rough;

    /// <summary>Height level (0..3) the Height tool paints.</summary>
    public int HeightLevel { get => _height; set => _height = Math.Clamp(value, 0, 3); }
    private int _height = 1;

    /// <summary>Overlay the Resource tool paints (Ashfall or Pitchblende).</summary>
    public OverlayKind Resource { get; set; } = OverlayKind.Ashfall;

    /// <summary>Resource density 1..12.</summary>
    public int ResourceDensity { get => _density; set => _density = Math.Clamp(value, 1, 12); }
    private int _density = 8;

    /// <summary>Blocking prop the Props tool paints (Tree, Wreck, Block, Pylon or Snag).</summary>
    public OverlayKind Prop { get => _prop; set { if (OverlayKinds.IsProp(value)) _prop = value; } }
    private OverlayKind _prop = OverlayKind.Tree;

    /// <summary>Sprite variant 0..2 of the prop (stored in the density byte).</summary>
    public int PropVariant { get => _propVariant; set => _propVariant = Math.Clamp(value, 0, 2); }
    private int _propVariant;

    /// <summary>Decal kind the Decal tool paints: 0 erases, otherwise a 1-based index into the theater's decal rules.</summary>
    public byte DecalKind { get; set; } = 1;

    /// <summary>Feature the Feature tool places.</summary>
    public FeatureType Feature { get; set; }

    /// <summary>Start slot 1..4 the Start tool places.</summary>
    public int StartSlot { get => _slot; set => _slot = Math.Clamp(value, 1, EditorDocument.MaxStarts); }
    private int _slot = 1;

    /// <summary>Brush size: 1, 3 or 5.</summary>
    public int BrushSize { get => _brush; set => _brush = BrushShape.Sizes.Contains(value) ? value : 1; }
    private int _brush = 1;

    /// <summary>True when the map changed since it was created, opened or saved.</summary>
    public bool Dirty { get; private set; }

    /// <summary>One line of feedback ("Start 3 needs a free slot"), empty when nothing to say.</summary>
    public string Status { get; private set; } = "";

    /// <summary>Sets the symmetry. Returns false (and keeps the old one) when the map is not square and the symmetry needs it.</summary>
    public bool SetSymmetry(MapSymmetry s)
    {
        if (!BrushShape.SymmetryFits(s, Document.Width, Document.Height))
        {
            Status = $"{s} needs a square map";
            return false;
        }
        if (Document.Symmetry != s) Dirty = true;
        Document.Symmetry = s;
        return true;
    }

    /// <summary>Marks the document as saved.</summary>
    public void MarkSaved() => Dirty = false;

    /// <summary>Marks the document as changed outside the step stack (name, biome).</summary>
    public void MarkChanged() => Dirty = true;

    // ---- strokes -----------------------------------------------------------------------------------------------

    /// <summary>Starts a stroke (mouse down). Ignored if one is open.</summary>
    public void BeginStroke()
    {
        if (_inStroke) return;
        _inStroke = true;
        _strokeCells.Clear();
        _strokeIndex.Clear();
        _startsBefore = null;
        _featuresBefore = null;
        _lastCell = null;
        _strokeLabel = Tool switch
        {
            EditorTool.Land => "Paint land", EditorTool.Height => "Paint height", EditorTool.Resource => "Paint resource",
            EditorTool.Feature => "Place feature", EditorTool.Start => "Place start", EditorTool.Eraser => "Erase", EditorTool.Props => "Paint prop", EditorTool.Decal => "Paint decal", EditorTool.Stamp => "Place stamp", _ => "Fill",
        };
    }

    /// <summary>Applies the current tool at a cell. While dragging, the cells between the previous call and this one are filled in too
    /// (paint-like tools only). Opens a stroke if none is open.</summary>
    public void ApplyAt(int x, int y)
    {
        if (!Document.Contains(x, y)) return;
        if (!_inStroke) BeginStroke();
        bool drag = Tool is EditorTool.Land or EditorTool.Height or EditorTool.Resource or EditorTool.Eraser or EditorTool.Props or EditorTool.Decal;
        if (drag && _lastCell is { } last && (last.X != x || last.Y != y))
        {
            foreach (var (lx, ly) in BrushShape.Line(last.X, last.Y, x, y).Skip(1)) ApplyOne(lx, ly);
        }
        else ApplyOne(x, y);
        _lastCell = (x, y);
    }

    /// <summary>Ends the stroke (mouse up) and records it. Returns true if it changed anything.</summary>
    public bool EndStroke()
    {
        if (!_inStroke) return false;
        _inStroke = false;
        bool startsChanged = _startsBefore is not null && !Document.Starts.SequenceEqual(_startsBefore);
        bool featuresChanged = _featuresBefore is not null && !Document.Features.SequenceEqual(_featuresBefore);
        var step = new EditorStep(_strokeLabel, _strokeCells.ToArray(),
            startsChanged ? _startsBefore : null, startsChanged ? Document.Starts.ToArray() : null,
            featuresChanged ? _featuresBefore : null, featuresChanged ? Document.Features.ToArray() : null);
        _strokeCells.Clear();
        _strokeIndex.Clear();
        _startsBefore = null;
        _featuresBefore = null;
        if (step.IsEmpty) return false;
        History.Push(step);
        Dirty = true;
        return true;
    }

    /// <summary>One click or whole stroke in a single call: begin, apply, end.</summary>
    public bool Click(int x, int y)
    {
        BeginStroke();
        ApplyAt(x, y);
        return EndStroke();
    }

    // ---- undo / redo -------------------------------------------------------------------------------------------

    /// <summary>Reverts the newest step. Returns false if there is none.</summary>
    public bool Undo()
    {
        if (_inStroke) EndStroke();
        EditorStep? s = History.PopUndo();
        if (s is null) return false;
        for (int i = s.Cells.Count - 1; i >= 0; i--) SetCell(s.Cells[i], undo: true);
        if (s.StartsBefore is not null) Document.SetStarts(s.StartsBefore);
        if (s.FeaturesBefore is not null) Document.SetFeatures(s.FeaturesBefore);
        Dirty = true;
        Status = "Undid: " + s.Label;
        return true;
    }

    /// <summary>Re-applies the newest undone step. Returns false if there is none.</summary>
    public bool Redo()
    {
        if (_inStroke) EndStroke();
        EditorStep? s = History.PopRedo();
        if (s is null) return false;
        foreach (CellEdit e in s.Cells) SetCell(e, undo: false);
        if (s.StartsAfter is not null) Document.SetStarts(s.StartsAfter);
        if (s.FeaturesAfter is not null) Document.SetFeatures(s.FeaturesAfter);
        Dirty = true;
        Status = "Redid: " + s.Label;
        return true;
    }

    private void SetCell(CellEdit e, bool undo)
    {
        Document.Land[e.Index] = undo ? e.OldLand : e.NewLand;
        Document.HeightLevel[e.Index] = undo ? e.OldHeight : e.NewHeight;
        Document.Overlay[e.Index] = undo ? e.OldOverlay : e.NewOverlay;
        Document.Density[e.Index] = undo ? e.OldDensity : e.NewDensity;
        Document.Decor[e.Index] = undo ? e.OldDecor : e.NewDecor;
    }

    // ---- tools -------------------------------------------------------------------------------------------------

    private void ApplyOne(int x, int y)
    {
        switch (Tool)
        {
            case EditorTool.Land:
            case EditorTool.Height:
            case EditorTool.Resource:
            case EditorTool.Props:
            case EditorTool.Decal:
                foreach (var (cx, cy) in BrushShape.Stroke(Document.Symmetry, Document.Width, Document.Height, x, y, BrushSize)) PaintCell(cx, cy);
                break;
            case EditorTool.Eraser:
                Erase(x, y);
                break;
            case EditorTool.Fill:
                Fill(x, y);
                break;
            case EditorTool.Start:
                PlaceStartSet(x, y);
                break;
            case EditorTool.Feature:
                PlaceFeatureSet(x, y);
                break;
            case EditorTool.Stamp:
                PlaceStamp(x, y);
                break;
        }
    }

    /// <summary>True when resources cannot lie on this land type.</summary>
    public static bool BlocksResources(LandType land) => land is LandType.Water or LandType.Rock or LandType.Cliff;

    /// <summary>True when blocking props cannot lie on this land type.</summary>
    public static bool BlocksProps(LandType land) => land is LandType.Water or LandType.Cliff;

    /// <summary>Why a prop cannot go on this cell, or null when it can: water or cliff, inside a start's 5x5 square, or on / beside a resource cell
    /// (the same rules the validator enforces as prop_in_start_square and prop_on_resource).</summary>
    public string? PropRefusal(int x, int y)
    {
        if (BlocksProps(Document.Land[Document.IndexOf(x, y)])) return "Props cannot go on water or cliffs";
        foreach (CellCoord s in Document.Starts)
            if (Math.Abs(s.X - x) <= 2 && Math.Abs(s.Y - y) <= 2) return "Props cannot go inside a start square";
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (Document.Contains(x + dx, y + dy) && OverlayKinds.IsResource(Document.Overlay[Document.IndexOf(x + dx, y + dy)]))
                    return "Props cannot go on or beside resources";
        return null;
    }

    private void Edit(int x, int y, LandType? land = null, byte? height = null, OverlayKind? overlay = null, byte? density = null, byte? decor = null)
    {
        int i = Document.IndexOf(x, y);
        LandType oldLand = Document.Land[i]; byte oldH = Document.HeightLevel[i]; OverlayKind oldO = Document.Overlay[i]; byte oldD = Document.Density[i]; byte oldDec = Document.Decor[i];
        LandType newLand = land ?? oldLand; byte newH = height ?? oldH; OverlayKind newO = overlay ?? oldO; byte newD = density ?? oldD; byte newDec = decor ?? oldDec;
        if (BlocksResources(newLand) && newO is OverlayKind.Ashfall or OverlayKind.Pitchblende) { newO = OverlayKind.None; newD = 0; }
        if (BlocksProps(newLand) && OverlayKinds.IsProp(newO)) { newO = OverlayKind.None; newD = 0; }
        if (newLand == oldLand && newH == oldH && newO == oldO && newD == oldD && newDec == oldDec) return;
        Document.Land[i] = newLand; Document.HeightLevel[i] = newH; Document.Overlay[i] = newO; Document.Density[i] = newD; Document.Decor[i] = newDec;
        if (_strokeIndex.TryGetValue(i, out int at))
        {
            CellEdit first = _strokeCells[at];
            _strokeCells[at] = first with { NewLand = newLand, NewHeight = newH, NewOverlay = newO, NewDensity = newD, NewDecor = newDec };
        }
        else
        {
            _strokeIndex[i] = _strokeCells.Count;
            _strokeCells.Add(new CellEdit(i, oldLand, newLand, oldH, newH, oldO, newO, oldD, newD, oldDec, newDec));
        }
    }

    private void PaintCell(int x, int y)
    {
        switch (Tool)
        {
            case EditorTool.Land: Edit(x, y, land: Land); break;
            case EditorTool.Height: Edit(x, y, height: (byte)HeightLevel); break;
            case EditorTool.Props:
                if (PropRefusal(x, y) is { } why) { Status = why; break; }
                Edit(x, y, overlay: Prop, density: (byte)PropVariant);
                break;
            case EditorTool.Decal: Edit(x, y, decor: DecalKind); break;
            case EditorTool.Resource:
                if (BlocksResources(Document.Land[Document.IndexOf(x, y)])) { Status = "Resources cannot go on water, rock or cliffs"; break; }
                Edit(x, y, overlay: Resource, density: (byte)ResourceDensity);
                break;
        }
    }

    private void Erase(int x, int y)
    {
        BeginTracking();
        foreach (var (cx, cy) in BrushShape.Stroke(Document.Symmetry, Document.Width, Document.Height, x, y, BrushSize))
        {
            int i = Document.IndexOf(cx, cy);
            if (Document.Overlay[i] is OverlayKind.Ashfall or OverlayKind.Pitchblende || OverlayKinds.IsProp(Document.Overlay[i])) Edit(cx, cy, overlay: OverlayKind.None, density: 0);
            if (Document.Decor[i] != 0) Edit(cx, cy, decor: 0);
            Document.Starts.RemoveAll(s => s.X == cx && s.Y == cy);
            Document.Features.RemoveAll(f => Covers(f, cx, cy));
        }
    }

    private bool Covers(MapFeature f, int x, int y)
    {
        FeatureType? t = FeatureTypes.FirstOrDefault(v => v.Id == f.Type);
        int w = t?.Width ?? 1, h = t?.Height ?? 1;
        return x >= f.Cell.X && y >= f.Cell.Y && x < f.Cell.X + w && y < f.Cell.Y + h;
    }

    private void BeginTracking()
    {
        _startsBefore ??= Document.Starts.ToList();
        _featuresBefore ??= Document.Features.ToList();
    }

    private void Fill(int x, int y)
    {
        var done = new bool[Document.Width * Document.Height];
        foreach (var (sx, sy) in BrushShape.Images(Document.Symmetry, Document.Width, Document.Height, x, y))
        {
            int start = Document.IndexOf(sx, sy);
            if (done[start]) continue;
            LandType target = Document.Land[start];
            if (target == Land) { done[start] = true; continue; }
            var stack = new Stack<(int, int)>();
            stack.Push((sx, sy));
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (!Document.Contains(cx, cy)) continue;
                int i = Document.IndexOf(cx, cy);
                if (done[i] || Document.Land[i] != target) continue;
                done[i] = true;
                Edit(cx, cy, land: Land);
                stack.Push((cx + 1, cy)); stack.Push((cx - 1, cy)); stack.Push((cx, cy + 1)); stack.Push((cx, cy - 1));
            }
        }
    }

    /// <summary>Places start <see cref="StartSlot"/> at the cell and its symmetric images as the next slots (design 08 §5).
    /// Refuses (with a <see cref="Status"/>) when it would pass 4 starts, when the cell is on the symmetry axis, or when it is already a start.</summary>
    private void PlaceStartSet(int x, int y)
    {
        var images = BrushShape.Images(Document.Symmetry, Document.Width, Document.Height, x, y);
        int order = MapSymmetries.Order(BrushShape.SymmetryFits(Document.Symmetry, Document.Width, Document.Height) ? Document.Symmetry : MapSymmetry.None);
        if (images.Count < order) { Status = "A start on the symmetry axis has no partner; move it off the axis"; return; }
        int first = Math.Min(StartSlot - 1, Document.Starts.Count);
        if (first + images.Count > EditorDocument.MaxStarts) { Status = $"Not enough start slots for {images.Count} symmetric starts from slot {first + 1}"; return; }
        var next = Document.Starts.ToList();
        for (int j = 0; j < images.Count; j++)
        {
            var cell = new CellCoord(images[j].X, images[j].Y);
            int clash = next.IndexOf(cell);
            if (clash >= 0 && (clash < first || clash >= first + images.Count)) { Status = "That cell is already another start"; return; }
        }
        BeginTracking();
        for (int j = 0; j < images.Count; j++)
        {
            var cell = new CellCoord(images[j].X, images[j].Y);
            if (first + j < next.Count) next[first + j] = cell; else next.Add(cell);
        }
        Document.SetStarts(next);
        int advance = first + images.Count + 1;
        StartSlot = advance <= EditorDocument.MaxStarts ? advance : 1;
        Status = $"Placed start {first + 1}" + (images.Count > 1 ? $"..{first + images.Count}" : "");
    }

    /// <summary>Applies the current stamp with its top-left footprint corner at the cell, as part of the open stroke (one undo step).
    /// With a declared symmetry and <see cref="StampSymmetric"/> the changed cells are copied to their images (skipping cells near a start or on resources).
    /// <see cref="Status"/> reports the refused cell count.</summary>
    private void PlaceStamp(int x, int y)
    {
        if (Stamp is not { } t) { Status = "No stamp fits this biome"; return; }
        EditorDocument d = Document;
        var canvas = new StampCanvas(d.Width, d.Height, (LandType[])d.Land.Clone(), (byte[])d.HeightLevel.Clone(), (OverlayKind[])d.Overlay.Clone(),
            (byte[])d.Density.Clone(), (byte[])d.Decor.Clone(), d.Starts);
        StampResult res = StampLibrary.Apply(t, canvas, x, y, StampRotation, StampMirror);
        var targets = new SortedSet<int>(res.Changed);
        var symmetry = BrushShape.SymmetryFits(d.Symmetry, d.Width, d.Height) ? d.Symmetry : MapSymmetry.None;
        int refused = res.Refused.Count;
        if (StampSymmetric && symmetry != MapSymmetry.None)
            foreach (int i in res.Changed)
            {
                var images = MapSymmetries.Images(symmetry, d.Width, d.Height, i % d.Width, i / d.Width);
                for (int k = 1; k < images.Length; k++)
                {
                    int j = images[k].Y * d.Width + images[k].X;
                    if (canvas.NearStart(images[k].X, images[k].Y) || OverlayKinds.IsResource(d.Overlay[j])) { refused++; continue; }
                    canvas.Land[j] = canvas.Land[i]; canvas.HeightLevel[j] = canvas.HeightLevel[i]; canvas.Overlay[j] = canvas.Overlay[i];
                    canvas.Density[j] = canvas.Density[i]; canvas.Decor![j] = canvas.Decor[i];
                    targets.Add(j);
                }
            }
        foreach (int i in targets)
            Edit(i % d.Width, i / d.Width, canvas.Land[i], canvas.HeightLevel[i], canvas.Overlay[i], canvas.Density[i], canvas.Decor![i]);
        Status = $"Stamped {t.Name}: {targets.Count} cell(s) changed" + (refused > 0 ? $", {refused} refused" : "");
    }

    /// <summary>Places the current feature (top-left at the cell) and, where its footprint still fits, its symmetric images.</summary>
    private void PlaceFeatureSet(int x, int y)
    {
        FeatureType t = Feature;
        int w = Document.Width, h = Document.Height;
        var symmetry = BrushShape.SymmetryFits(Document.Symmetry, w, h) ? Document.Symmetry : MapSymmetry.None;
        if (!FootprintInside(x, y, t.Width, t.Height)) { Status = "The feature does not fit on the map there"; return; }
        var a = MapSymmetries.Images(symmetry, w, h, x, y);
        var b = MapSymmetries.Images(symmetry, w, h, x + t.Width - 1, y + t.Height - 1);
        var rects = new List<(int X, int Y)>();
        int skipped = 0;
        for (int j = 0; j < a.Length; j++)
        {
            int rx = Math.Min(a[j].X, b[j].X), ry = Math.Min(a[j].Y, b[j].Y);
            int rw = Math.Abs(a[j].X - b[j].X) + 1, rh = Math.Abs(a[j].Y - b[j].Y) + 1;
            if (rw != t.Width || rh != t.Height) { skipped++; continue; }
            if (!rects.Contains((rx, ry))) rects.Add((rx, ry));
        }
        var placed = new List<MapFeature>();
        foreach (var (rx, ry) in rects)
        {
            if (Overlaps(rx, ry, t, placed)) { Status = "Features may not overlap"; return; }
            placed.Add(new MapFeature(t.Id, new CellCoord(rx, ry)));
        }
        BeginTracking();
        Document.Features.AddRange(placed);
        Status = skipped > 0 ? $"Placed {placed.Count} {t.Id}; {skipped} rotated image(s) skipped (footprint does not turn)" : $"Placed {placed.Count} {t.Id}";
    }

    private bool FootprintInside(int x, int y, int fw, int fh) => x >= 0 && y >= 0 && x + fw <= Document.Width && y + fh <= Document.Height;

    private bool Overlaps(int x, int y, FeatureType t, List<MapFeature> also)
    {
        bool Hit(MapFeature f)
        {
            FeatureType? o = FeatureTypes.FirstOrDefault(v => v.Id == f.Type);
            int ow = o?.Width ?? 1, oh = o?.Height ?? 1;
            return x < f.Cell.X + ow && f.Cell.X < x + t.Width && y < f.Cell.Y + oh && f.Cell.Y < y + t.Height;
        }
        return Document.Features.Any(Hit) || also.Any(Hit);
    }
}
