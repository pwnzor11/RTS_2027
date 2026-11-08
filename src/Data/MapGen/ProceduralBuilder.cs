// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// One attempt of the procedural generator (design 08 §1): paints a fundamental domain through <see cref="GenGrid"/> so every write
/// lands on all symmetry images. Order: start reserves, ocean, plateaus, rivers, roads, ponds, lagoons, rock, rough, fields, ruins,
/// start squares, shore rings, blocking props (M12-05, <see cref="DecorateProps"/>). Uses only <see cref="SimRandom"/> and integer maths.
/// </summary>
internal sealed partial class ProceduralBuilder
{
    /// <summary>Chebyshev radius of the clear, buildable start square (5×5).</summary>
    public const int StartSquareRadius = 2;
    private const int CoreRadius = 3;      // resources keep out
    private const int ReserveRadius = 8;   // terrain features keep out
    private const int PlainInset = 6;
    private const int RuinStartDistance = 9;

    private readonly BiomeParams _b;
    private readonly GenGrid _g;
    private readonly SimRandom _rng;
    private readonly int _n;
    private readonly int _scale;   // map size relative to Medium, in percent
    private readonly bool _decorate;   // M12-05: the prop pass (Layout 1)
    private readonly List<MapFeature> _features = new();
    private readonly OceanEdgePass? _ocean;   // M7-13: null unless the ocean edge option is on

    /// <summary>All starts of the symmetry orbit (4 for Rot4, 2 otherwise), start 0 first.</summary>
    public CellCoord[] OrbitStarts { get; }

    /// <summary>Convoy spawn cells (<c>convoy_edge</c>) of the ocean edge option, filled by <see cref="Build"/>; empty when it is off.</summary>
    public IReadOnlyList<CellCoord> ConvoyEdge { get; private set; } = Array.Empty<CellCoord>();

    /// <summary>Landing beach markers (<c>landing</c>) of the ocean edge option, filled by <see cref="Build"/>; empty when it is off.</summary>
    public IReadOnlyList<CellCoord> Landing { get; private set; } = Array.Empty<CellCoord>();

    public ProceduralBuilder(BiomeParams biome, int n, MapSymmetry symmetry, ulong seed, OceanEdges oceanEdges = OceanEdges.None, bool decorate = true)
    {
        _b = biome; _n = n; _scale = n * 100 / 96; _decorate = decorate;
        _g = new GenGrid(n, symmetry, biome.BaseLand);
        _rng = new SimRandom(seed);
        int inset = biome.OceanWidth > 0 ? OceanWidth() + 7 : PlainInset;
        if (oceanEdges != OceanEdges.None)
        {
            // The sea, its shallows and the beach sit between the edge and the start squares.
            _ocean = new OceanEdgePass(_g, _rng, oceanEdges, EdgeDeepWidth());
            inset = Math.Max(inset, OceanEdgePass.MaxBand(EdgeDeepWidth()) + 3);
        }
        (int sx, int sy) = symmetry switch
        {
            MapSymmetry.MirrorX => (inset, n / 2 - 1),
            MapSymmetry.MirrorY => (n / 2 - 1, inset),
            _ => (inset, inset),
        };
        var img = MapSymmetries.Images(symmetry, n, n, sx, sy);
        OrbitStarts = img.Select(c => new CellCoord(c.X, c.Y)).ToArray();
    }

    /// <summary>Neutral features placed (list order = placement order).</summary>
    public IReadOnlyList<MapFeature> Features => _features;

    public MapData Build(string name, IReadOnlyList<CellCoord> starts)
    {
        if (_zoned) return BuildZoned(name, starts);   // layout 2 (M12-07)
        Reserve();
        if (_b.OceanWidth > 0) Ocean();
        _ocean?.Paint();
        for (int i = 0; i < _b.CliffPlateaus; i++) Plateau();
        for (int i = 0; i < _b.Rivers; i++) River(i);
        if (_b.RoadGrid > 0) Roads();
        Budget(_b.WaterPermille, () => Blob(_rng.Next(2, 4), LandType.Water));
        Budget(_b.ShallowPermille, () => Blob(_rng.Next(2, 4), LandType.Shallow));
        Budget(_b.RockPermille, RockFeature);
        Budget(_b.RoughPermille, () => Blob(_rng.Next(1, 3), LandType.Rough));
        Fields();
        Ruins();
        StartSquares();
        ShoreRings();
        int c = _n * _n;
        var map = new MapData(name, _b.Theater, _n, _n, new ushort[c], _g.Land, _g.Height, _g.Overlay, _g.Density, starts);
        if (_decorate) DecorateProps(map);   // M12-05: last of the land passes; the arrays are shared with the map
        if (_ocean is not null)
        {
            _ocean.PaintCoast();   // the arrays are shared with the map, so the beach shows in it
            (List<CellCoord> convoy, List<CellCoord> landing) = _ocean.Markers(map, starts, MapAnalysis.FeatureMask(map, _features));
            ConvoyEdge = convoy; Landing = landing;
        }
        return map;
    }

    private int Scaled(int v) => v * _scale / 100;
    private int EdgeDeepWidth() => Math.Max(3, Scaled(4));
    private int OceanWidth() => Math.Max(3, Scaled(_b.OceanWidth));
    private static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    private int MinStartDistance(int x, int y)
    {
        int best = int.MaxValue;
        foreach (CellCoord s in OrbitStarts) best = Math.Min(best, Cheb(x, y, s.X, s.Y));
        return best;
    }

    // ---- start reserves -------------------------------------------------------------------------

    private void Reserve()
    {
        CellCoord s = OrbitStarts[0];
        for (int dy = -ReserveRadius; dy <= ReserveRadius; dy++)
            for (int dx = -ReserveRadius; dx <= ReserveRadius; dx++)
            {
                int x = s.X + dx, y = s.Y + dy;
                if (!_g.In(x, y)) continue;
                int i = _g.Idx(x, y);
                _g.MarkReserved(i);
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= CoreRadius) _g.MarkCore(i);
            }
    }

    // ---- ocean (Glass Coast) --------------------------------------------------------------------

    /// <summary>Ocean on all four edges: deep Water of width ow + a wobble 0..2, then a two-cell Shallow band. Each edge gets its own
    /// smoothed wobble; orbit writes keep it symmetric. Shallow is painted first so later Water wins.</summary>
    private void Ocean()
    {
        int ow = OceanWidth();
        var depth = new int[4][];
        for (int e = 0; e < 4; e++)
        {
            depth[e] = new int[_n];
            int d = 1;
            for (int t = 0; t < _n; t++)
            {
                if (_rng.Next(3) == 0) d = Math.Clamp(d + _rng.Next(-1, 2), 0, 2);
                depth[e][t] = ow + d;
            }
        }
        for (int pass = 0; pass < 2; pass++)
            for (int e = 0; e < 4; e++)
                for (int t = 0; t < _n; t++)
                {
                    int limit = depth[e][t] + (pass == 0 ? 2 : 0);
                    for (int k = 0; k < limit; k++)
                    {
                        (int x, int y) = e switch { 0 => (t, k), 1 => (_n - 1 - k, t), 2 => (t, _n - 1 - k), _ => (k, t) };
                        int i = _g.Idx(x, y);
                        if (pass == 0) { if (_g.All(i, j => _g.Land[j] != LandType.Water)) _g.SetLand(i, LandType.Shallow); }
                        else _g.SetLand(i, LandType.Water);
                    }
                }
    }

    // ---- plateaus (Mountains) -------------------------------------------------------------------

    private static bool InOctagon(int dx, int dy, int r) => Math.Max(Math.Abs(dx), Math.Abs(dy)) <= r && Math.Abs(dx) + Math.Abs(dy) <= r + r / 2;

    private void Plateau()
    {
        int r = _rng.Next(Math.Max(5, _n / 12), _n / 8 + 1);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int cx = _rng.Next(r + 3, _n - r - 3), cy = _rng.Next(r + 3, _n - r - 3);
            if (!PlateauFits(cx, cy, r)) continue;
            Octagon(cx, cy, r, 1, 2);
            if (r >= 9) Octagon(cx, cy, r - 4, 2, 1);
            return;
        }
    }

    private bool PlateauFits(int cx, int cy, int r)
    {
        int i0 = _g.Idx(cx, cy);
        var imgs = _g.Images(i0);
        for (int k = 1; k < imgs.Length; k++)
            if (Cheb(cx, cy, imgs[k] % _n, imgs[k] / _n) <= 2 * r + 6) return false;
        for (int dy = -r - 3; dy <= r + 3; dy++)
            for (int dx = -r - 3; dx <= r + 3; dx++)
                if (!_g.AllFree(_g.Idx(cx + dx, cy + dy))) return false;
        return true;
    }

    /// <summary>Raises an octagon to <paramref name="level"/> with a Cliff boundary and <paramref name="ramps"/> 2–4 cell ramps on
    /// distinct sides; the cells just outside each ramp are kept clear.</summary>
    private void Octagon(int cx, int cy, int r, byte level, int ramps)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (!InOctagon(dx, dy, r)) continue;
                int i = _g.Idx(cx + dx, cy + dy);
                _g.SetHeight(i, level);
                bool edge = !InOctagon(dx + 1, dy, r) || !InOctagon(dx - 1, dy, r) || !InOctagon(dx, dy + 1, r) || !InOctagon(dx, dy - 1, r);
                _g.SetLand(i, edge ? LandType.Cliff : _b.BaseLand);
            }
        int first = _rng.Next(4);
        for (int k = 0; k < ramps; k++)
        {
            int side = (first + k * 2) % 4;   // opposite sides
            int w = _rng.Next(2, 5);
            int a = _rng.Next(-r / 2, r / 2 - w + 2);
            for (int t = a; t < a + w; t++)
                for (int o = 0; o <= 3; o++)   // the cliff cell and the approach outside it
                {
                    (int dx, int dy) = side switch { 0 => (t, -r - o), 1 => (r + o, t), 2 => (t, r + o), _ => (-r - o, t) };
                    int x = cx + dx, y = cy + dy;
                    if (!_g.In(x, y)) continue;
                    int i = _g.Idx(x, y);
                    if (o == 0) _g.SetLand(i, _b.BaseLand);
                    _g.MarkRamp(i);
                }
        }
    }

    // ---- rivers ---------------------------------------------------------------------------------

    /// <summary>
    /// A river along the axis between the starts: from the middle of an edge to the centre for rotations (the images complete it), or
    /// edge to edge for mirrors. Banks are Shallow; crossings every ford_spacing..1.5× alternate Road bridge (first) and Shallow ford.
    /// River index 1 runs on the other axis.
    /// </summary>
    private void River(int index)
    {
        bool mirror = _g.Symmetry is MapSymmetry.MirrorX or MapSymmetry.MirrorY;
        // Horizontal river (travels along x) unless the symmetry or index says vertical.
        bool vertical = _g.Symmetry == MapSymmetry.MirrorX;
        if (index == 1) vertical = !vertical;
        int c = _n / 2 - 1;
        int length = mirror ? _n : _n / 2;
        int bound = Math.Max(0, Math.Min(_n / 8, _n / 2 - PlainInset - ReserveRadius - _b.RiverWidth - 2));
        var path = new List<(int Along, int Lat)>();
        int lat = _b.RiverWanderPermille > 0 ? _rng.Next(-bound / 2, bound / 2 + 1) : 0;
        for (int s = 0; s < length; s++)
        {
            int prev = lat;
            int remaining = length - 1 - s;
            if (!mirror && Math.Abs(lat) >= remaining && lat != 0) lat -= Math.Sign(lat);
            else if (_rng.Next(1000) < _b.RiverWanderPermille) lat = Math.Clamp(lat + (_rng.Next(2) == 0 ? -1 : 1), -bound, bound);
            if (lat != prev && s > 0) path.Add((s, prev));
            path.Add((s, lat));
        }
        int r = _b.RiverWidth;
        (int X, int Y) At(int along, int la) => vertical ? (c + la, along) : (along, c + la);
        for (int pass = 0; pass < 2; pass++)
            foreach ((int along, int la) in path)
            {
                (int px, int py) = At(along, la);
                int rad = pass == 0 ? r : r - 1;
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int x = px + dx, y = py + dy;
                        if (!_g.In(x, y)) continue;
                        int i = _g.Idx(x, y);
                        if (!_g.All(i, j => !_g.Reserved[j] && _g.Height[j] == 0)) continue;
                        if (pass == 0 && !_g.All(i, j => _g.Land[j] != LandType.Water)) continue;
                        _g.SetLand(i, pass == 0 ? LandType.Shallow : LandType.Water);
                        _g.MarkRiver(i);
                    }
            }

        // Crossings.
        int spacing = _b.FordSpacing;
        int end = mirror ? length - 3 : length - 4;
        var positions = new List<int>();
        for (int pos = spacing / 2 + _rng.Next(spacing / 2 + 1); pos < end; pos += spacing + _rng.Next(spacing / 2 + 1)) positions.Add(pos);
        if (positions.Count == 0) positions.Add(length / 2);
        for (int k = 0; k < positions.Count; k++)
        {
            bool bridge = k % 2 == 0;
            int at = positions[k];
            int la = path.Last(p => p.Along == at).Lat;
            int width = bridge ? 2 : 3;
            for (int a = at; a < at + width && a < length; a++)
                for (int l = la - r - 2; l <= la + r + 2; l++)
                {
                    (int x, int y) = At(a, l);
                    if (!_g.In(x, y)) continue;
                    int i = _g.Idx(x, y);
                    if (!_g.River[i]) continue;
                    if (bridge) _g.SetLand(i, LandType.Road);
                    else if (_g.Land[i] == LandType.Water) _g.SetLand(i, LandType.Shallow);
                }
        }
    }

    // ---- road grid (Rust Belt) ------------------------------------------------------------------

    /// <summary>Two-cell roads on lines symmetric about the centre every road_grid cells, in both directions. Roads stop at rivers
    /// (the river's own bridges cross them) and skip ocean, cliffs and plateaus.</summary>
    private void Roads()
    {
        var lines = new List<int> { _n / 2 - 1, _n / 2 };
        for (int k = 1; ; k++)
        {
            int a = _n / 2 - 1 - k * _b.RoadGrid, b = _n / 2 + k * _b.RoadGrid;
            if (a - 1 < 1) break;
            lines.Add(a); lines.Add(a - 1); lines.Add(b); lines.Add(b + 1);
        }
        foreach (int line in lines)
            for (int t = 0; t < _n; t++)
                for (int dir = 0; dir < 2; dir++)
                {
                    int i = dir == 0 ? _g.Idx(t, line) : _g.Idx(line, t);
                    if (_g.All(i, j => _g.Height[j] == 0 && !_g.Ramp[j] && !_g.River[j] && (_g.Land[j] == _b.BaseLand || _g.Land[j] == LandType.Road)))
                        _g.SetLand(i, LandType.Road);
                }
    }

    // ---- scatter features -----------------------------------------------------------------------

    private void Budget(int permille, Func<int> paint)
    {
        int target = permille * _n * _n / 1000, done = 0;
        for (int attempt = 0; attempt < 600 && done < target; attempt++) done += paint();
    }

    private (int X, int Y) RandomCell() => (_rng.Next(_n), _rng.Next(_n));

    /// <summary>Diamond blob (Manhattan radius r, ragged rim) at a random free spot.</summary>
    private int Blob(int r, LandType land)
    {
        (int cx, int cy) = RandomCell();
        if (!_g.AllFree(_g.Idx(cx, cy))) return 0;
        int c = 0;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int d = Math.Abs(dx) + Math.Abs(dy);
                if (d > r || (d == r && r > 0 && _rng.Next(3) == 0)) continue;
                c += _g.PaintFree(cx + dx, cy + dy, land);
            }
        return c;
    }

    private int RockFeature() => _b.RockStyle switch
    {
        "rubble" => Rubble(),
        "dunes" => Ridge(_rng.Next(Scaled(10), Scaled(22) + 1), dune: true),
        "spurs" => Ridge(_rng.Next(Scaled(6), Scaled(16) + 1), dune: false),
        _ => Blob(_rng.Next(1, 3), LandType.Rock),
    };

    /// <summary>A rubble block: a Rough rectangle with Rock in its interior.</summary>
    private int Rubble()
    {
        int w = _rng.Next(3, 7), h = _rng.Next(3, 7);
        (int x0, int y0) = RandomCell();
        int c = 0;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                bool inner = x > x0 && y > y0 && x < x0 + w - 1 && y < y0 + h - 1;
                c += _g.PaintFree(x, y, inner && _rng.Next(3) != 0 ? LandType.Rock : LandType.Rough);
            }
        return c;
    }

    /// <summary>A wandering ridge: a Rock spine (spurs), or for dunes Rock "glass" crests with gaps inside a Rough band. The spine is
    /// walked first and painted before the band, so the band never covers a crest.</summary>
    private int Ridge(int length, bool dune)
    {
        (int x, int y) = RandomCell();
        int dir = _rng.Next(8), c = 0;
        var spine = new List<(int X, int Y)>();
        for (int s = 0; s < length && _g.In(x, y); s++)
        {
            spine.Add((x, y));
            if (_rng.Next(4) == 0) dir = (dir + (_rng.Next(2) == 0 ? 1 : 7)) % 8;
            x += dir is 1 or 2 or 3 ? 1 : dir is 5 or 6 or 7 ? -1 : 0;
            y += dir is 3 or 4 or 5 ? 1 : dir is 7 or 0 or 1 ? -1 : 0;
        }
        foreach ((int sx, int sy) in spine)
        {
            if (dune) { if (_rng.Next(3) != 0) c += _g.PaintFree(sx, sy, LandType.Rock); }
            else
            {
                c += _g.PaintFree(sx, sy, LandType.Rock);
                if (_rng.Next(3) == 0) c += _g.PaintFree(sx + 1, sy, LandType.Rock);
            }
        }
        if (dune)   // the band does not count against rock_permille
            foreach ((int sx, int sy) in spine)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        _g.PaintFree(sx + dx, sy + dy, LandType.Rough);
        return c;
    }

    // ---- resource fields ------------------------------------------------------------------------

    private bool FieldCell(int j) =>
        !_g.Core[j] && _g.Height[j] == 0 && !_g.River[j] && !_g.Feature[j] && !_g.Ramp[j]
        && _g.Overlay[j] == OverlayKind.None && _g.Land[j] is LandType.Clear or LandType.Ash;

    private byte Density() => (byte)_rng.Next(_b.FieldDensityMin, _b.FieldDensityMax + 1);

    /// <summary>Ashfall blob (Manhattan radius r) on Ash land, or a 2×2 Pitchblende pit on Clear. Returns cells placed.</summary>
    private int Field(int cx, int cy, bool pitch, int r)
    {
        if (!_g.In(cx, cy) || !_g.All(_g.Idx(cx, cy), FieldCell)) return 0;
        int c = 0;
        if (pitch)
        {
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (_g.In(x, y) && _g.All(_g.Idx(x, y), FieldCell)) c += _g.SetResource(_g.Idx(x, y), LandType.Clear, OverlayKind.Pitchblende, Density());
                }
            return c;
        }
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int d = Math.Abs(dx) + Math.Abs(dy);
                if (d > r || (d == r && r > 0 && _rng.Next(3) == 0)) continue;
                int x = cx + dx, y = cy + dy;
                if (_g.In(x, y) && _g.All(_g.Idx(x, y), FieldCell)) c += _g.SetResource(_g.Idx(x, y), LandType.Ash, OverlayKind.Ashfall, Density());
            }
        return c;
    }

    private void Fields()
    {
        CellCoord s = OrbitStarts[0];
        // Start fields: centre at Chebyshev 5..8 from start 0, inside the cleared reserve (the images give every start the same).
        int total = _b.PitchFieldsPerStart + _b.AshFieldsPerStart;
        for (int f = 0; f < total; f++)
        {
            bool pitch = f < _b.PitchFieldsPerStart;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int x = s.X + _rng.Next(-8, 9), y = s.Y + _rng.Next(-8, 9);
                int d = Cheb(x, y, s.X, s.Y);
                if (d < (pitch ? 5 : 6) || d > 8 || Math.Abs(x - s.X) + Math.Abs(y - s.Y) > 11) continue;
                if (Field(x, y, pitch, _rng.Next(1, 3)) > 0) break;
            }
        }
        // Contested fields: 40..60 % of the way from start 0 to the centre, jittered.
        int mx = _n / 2 - 1, my = _n / 2 - 1;
        for (int f = 0; f < _b.ContestedFields; f++)
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int t = _rng.Next(40, 61);
                int x = s.X + (mx - s.X) * t / 100 + _rng.Next(-3, 4), y = s.Y + (my - s.Y) * t / 100 + _rng.Next(-3, 4);
                if (MinStartDistance(x, y) < 10) continue;
                if (Field(x, y, f == 0, 2) > 0) break;
            }
        // Extra fields anywhere away from the starts (Rust Belt: next to a road).
        for (int f = 0; f < _b.ExtraFields; f++)
            for (int attempt = 0; attempt < 80; attempt++)
            {
                (int x, int y) = RandomCell();
                if (MinStartDistance(x, y) < 12 || (_b.RoadGrid > 0 && !NearRoad(x, y))) continue;
                if (Field(x, y, false, _rng.Next(1, 3)) > 0) break;
            }
    }

    private bool NearRoad(int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (_g.In(x + dx, y + dy) && _g.Land[_g.Idx(x + dx, y + dy)] == LandType.Road) return true;
        return false;
    }

    // ---- neutral ruins (Q-M4-b) -----------------------------------------------------------------

    /// <summary>Places `ruins` neutral ruins per orbit on buildable free ground at least 9 cells from every start. A non-square
    /// footprint does not survive a quarter turn, so Rot4 / MirrorDiag maps use only the square depot.</summary>
    private void Ruins()
    {
        bool squareOnly = _g.Symmetry is MapSymmetry.Rot4 or MapSymmetry.MirrorDiag;
        for (int r = 0; r < _b.Ruins; r++)
        {
            string type = !squareOnly && r % 2 == 0 ? "neu_ruin_tenement" : "neu_ruin_depot";
            (int fw, int fh) = MapAnalysis.FootprintOf(type);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                (int x0, int y0) = RandomCell();
                if (x0 < 1 || y0 < 1 || x0 + fw >= _n - 1 || y0 + fh >= _n - 1) continue;
                if (MinStartDistance(x0, y0) < RuinStartDistance + 2 || !RuinFits(x0, y0, fw, fh)) continue;
                PlaceRuin(type, x0, y0, fw, fh);
                break;
            }
        }
    }

    private bool RuinFits(int x0, int y0, int fw, int fh)
    {
        var cells = new List<int>();
        for (int y = y0 - 1; y <= y0 + fh; y++)
            for (int x = x0 - 1; x <= x0 + fw; x++)
            {
                int i = _g.Idx(x, y);
                if (!_g.All(i, j => _g.Free(j) || _g.Land[j] == LandType.Road)) return false;
                if (_zoned && !_g.All(i, j => _zone[j] != ZLane)) return false;   // layout 2 keeps its lanes clear
                if (x >= x0 && y >= y0 && x < x0 + fw && y < y0 + fh) cells.Add(i);
            }
        // The footprint's images must not overlap each other (or touch: the 1-cell margin).
        var mark = new bool[_n * _n];
        foreach (int i in cells) mark[i] = true;
        foreach (int i in cells)
        {
            var imgs = _g.Images(i);
            for (int k = 1; k < imgs.Length; k++)
            {
                int ix = imgs[k] % _n, iy = imgs[k] / _n;
                if (ix >= x0 - 2 && iy >= y0 - 2 && ix <= x0 + fw + 1 && iy <= y0 + fh + 1) return false;
            }
        }
        return true;
    }

    private void PlaceRuin(string type, int x0, int y0, int fw, int fh)
    {
        int k = _g.K;
        var minX = new int[k]; var minY = new int[k];
        Array.Fill(minX, int.MaxValue); Array.Fill(minY, int.MaxValue);
        for (int y = y0; y < y0 + fh; y++)
            for (int x = x0; x < x0 + fw; x++)
            {
                int i = _g.Idx(x, y);
                if (_g.Land[i] == LandType.Road) _g.SetLand(i, _b.BaseLand);
                var imgs = _g.Images(i);
                for (int q = 0; q < k; q++)
                {
                    _g.Feature[imgs[q]] = true;
                    minX[q] = Math.Min(minX[q], imgs[q] % _n); minY[q] = Math.Min(minY[q], imgs[q] / _n);
                }
            }
        for (int q = 0; q < k; q++) _features.Add(new MapFeature(type, new CellCoord(minX[q], minY[q])));
    }

    // ---- start squares and shore rings ----------------------------------------------------------

    private void StartSquares()
    {
        CellCoord s = OrbitStarts[0];
        for (int dy = -StartSquareRadius; dy <= StartSquareRadius; dy++)
            for (int dx = -StartSquareRadius; dx <= StartSquareRadius; dx++)
            {
                int i = _g.Idx(s.X + dx, s.Y + dy);
                _g.SetLand(i, _b.BaseLand);
                _g.SetHeight(i, 0);
            }
    }

    /// <summary>Every land cell 8-adjacent to deep Water becomes Shallow (design 08 §1: M7 can re-tag shores without moving a
    /// coastline). Roads (bridges), Cliffs, start cores and feature footprints are left alone.</summary>
    private void ShoreRings()
    {
        int n = _n;
        var convert = new bool[n * n];
        for (int i = 0; i < n * n; i++)
        {
            if (_g.Land[i] != LandType.Water) continue;
            int x = i % n, y = i / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!_g.In(nx, ny)) continue;
                    int j = _g.Idx(nx, ny);
                    LandType l = _g.Land[j];
                    if (l is LandType.Water or LandType.Shallow or LandType.Road or LandType.Cliff || _g.Core[j] || _g.Feature[j]) continue;
                    convert[j] = true;
                }
        }
        for (int i = 0; i < n * n; i++) if (convert[i]) _g.SetLand(i, LandType.Shallow);
    }
}
