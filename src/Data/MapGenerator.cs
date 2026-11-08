// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Synthetic skirmish map generator for tests and early playtests. Deterministic for (size, seed), uses only
/// <see cref="SimRandom"/>, and is two-fold rotationally symmetric (cell i mirrors to Width*Height-1-i).
/// All numeric parameters below are placeholders pending design sign-off.
/// </summary>
public static class MapGenerator
{
    /// <summary>Smallest size that fits the starts, the plateau and the features.</summary>
    public const int MinSize = 24;
    /// <summary>The generator version (<see cref="MapGenParams.Layout"/>) this build produces and can rebuild. 0 = the generator before M12
    /// (D-180: replaced outright, so a save or map file that names 0 cannot be rebuilt); 1 = the M12 generator with its decoration pass.</summary>
    public const int CurrentLayout = 1;

    /// <summary>The zone-skeleton generator (M12-07, D-183), built for Rust Belt only; opt in with <c>MapGenParams.Layout = 2</c>.</summary>
    public const int ZonedLayout = 2;

    /// <summary>Attempts allowed for the zoned layout (its checks are stricter than layout 1's).</summary>
    public const int MaxZonedAttempts = 40;

    /// <summary>The friendly message of a save made with a generator this build no longer has.</summary>
    public const string OldGeneratorMessage = "This save was made with an older map generator and can't be loaded.";

    /// <summary>How many seeds (seed, seed+1, ...) are tried before giving up on connectivity.</summary>
    public const int MaxAttempts = 10;

    private const int StartInset = 4;      // start position = (4,4) and its mirror
    private const int StartClearRadius = 3; // Chebyshev radius forced to clear ground
    private const int StartKeepRadius = 5;  // features keep out of this radius (pits excepted)

    /// <summary>Cells per side of a generated map size: Small 64, Medium 96, Large 128 (design 08 §1, M4).</summary>
    public static int CellsFor(MapSize size) => size switch
    {
        MapSize.Small => 64,
        MapSize.Medium => 96,
        MapSize.Large => SimConstants.MaxMapCells,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    /// <summary>The default name of a generated map: <c>&lt;biome&gt;_&lt;s|m|l&gt;_&lt;players&gt;p_&lt;seed&gt;</c>, e.g. rust_belt_m_4p_77.</summary>
    public static string DefaultName(MapGenParams p)
    {
        ArgumentNullException.ThrowIfNull(p);
        string biome = p.Biome switch
        {
            Biome.RustBelt => "rust_belt", Biome.GlassCoast => "glass_coast", Biome.Mountains => "mountains", Biome.AshPlains => "ash_plains",
            _ => throw new ArgumentOutOfRangeException(nameof(p)),
        };
        string size = p.Size switch { MapSize.Small => "s", MapSize.Medium => "m", MapSize.Large => "l", _ => throw new ArgumentOutOfRangeException(nameof(p)) };
        return $"{biome}_{size}_{p.Players}p_{p.Seed}";
    }

    /// <summary>
    /// Generates a map from biome, size, players and seed (design 08 §1, M4-02), with the biome parameters of
    /// <c>data/mapgen/biomes.toml</c> (<see cref="BiomeCatalog.Default"/>). See <see cref="GenerateWithMeta(MapGenParams, BiomeParams)"/>.
    /// </summary>
    public static MapData Generate(MapGenParams p) => GenerateWithMeta(p).Map;

    /// <summary>As <see cref="Generate(MapGenParams)"/>, with explicit biome parameters (tests and tools).</summary>
    public static MapData Generate(MapGenParams p, BiomeParams biome) => GenerateWithMeta(p, biome).Map;

    /// <summary>As <see cref="Generate(MapGenParams)"/>, plus the map metadata (biome, symmetry, max players, provenance, ruins).</summary>
    public static (MapData Map, MapMeta Meta) GenerateWithMeta(MapGenParams p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (!Enum.IsDefined(p.Biome)) throw new ArgumentOutOfRangeException(nameof(p), "Unknown biome.");
        return GenerateWithMeta(p, BiomeCatalog.For(p.Biome));
    }

    /// <summary>
    /// Generates a map with explicit biome parameters. The output depends only on (params, biome values): <see cref="SimRandom"/>,
    /// integer maths and cell-index scans. 2 players use the biome's 2-player symmetry; 3 and 4 players use Rot4 (Q-M4-a option A:
    /// with 3 players one of the four starts, chosen by seed, stays empty and its fields become a contested expansion). Each attempt
    /// (seed, seed+1, …, up to <see cref="MaxAttempts"/>) must pass the §1 checks on all symmetry starts with the built-in passability
    /// rule: Foot + Tracked connectivity, ≥ 2 fields within 14 cells of path, exact path equality, a 5×5 buildable start square and
    /// ≥ 12 cells between starts. Throws <see cref="InvalidOperationException"/> when no attempt passes.
    /// </summary>
    public static (MapData Map, MapMeta Meta) GenerateWithMeta(MapGenParams p, BiomeParams biome) => GenerateWithMeta(p, biome, out _);

    /// <summary>As above; <paramref name="attempts"/> is how many seeds were tried (1 = the first passed).</summary>
    public static (MapData Map, MapMeta Meta) GenerateWithMeta(MapGenParams p, BiomeParams biome, out int attempts)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(biome);
        attempts = 1;
        if (p.Players < 2 || p.Players > 4) throw new ArgumentOutOfRangeException(nameof(p), "Players must be 2..4.");
        if (p.Layout == ZonedLayout) return GenerateZoned(p, biome, out attempts);
        if (p.Layout != CurrentLayout) throw new InvalidOperationException(OldGeneratorMessage);   // D-180 D4: the old generator is gone
        int n = CellsFor(p.Size);
        string name = p.Name ?? DefaultName(p);
        MapSymmetry symmetry = p.Players == 2 ? biome.Symmetry2p : biome.Symmetry4p;
        if (p.Players > 2 && symmetry != MapSymmetry.Rot4) throw new ArgumentException("3 or 4 players need rot4 symmetry_4p", nameof(biome));
        string? last = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            ulong seed = p.Seed + (ulong)attempt;
            var builder = new ProceduralBuilder(biome, n, symmetry, seed, p.OceanEdges);
            CellCoord[] orbit = builder.OrbitStarts;
            MapData full = builder.Build(name, orbit);
            var fullMeta = new MapMeta(biome.Key, symmetry, orbit.Length, null, builder.Features.ToArray())
                { ConvoyEdge = builder.ConvoyEdge, Landing = builder.Landing };
            last = FirstFailure(full, fullMeta);
            if (last is not null) continue;

            CellCoord[] starts = orbit;
            if (p.Players == 3)
            {
                int empty = (int)(p.Seed % 4UL);   // slot-to-start assignment rotated by seed
                starts = new CellCoord[3];
                for (int k = 0; k < 3; k++) starts[k] = orbit[(empty + 1 + k) % 4];
            }
            var map = new MapData(name, full.Theater, n, n, full.Template.ToArray(), full.Land.ToArray(), full.HeightLevel.ToArray(),
                full.Overlay.ToArray(), full.Density.ToArray(), starts);
            var meta = new MapMeta(biome.Key, symmetry, p.Players, p with { Name = null }, builder.Features.ToArray())
                { ConvoyEdge = builder.ConvoyEdge, Landing = builder.Landing };
            return (map, meta);
        }
        throw new InvalidOperationException(
            $"No valid {biome.Key} map for size {n}, {p.Players} players from seed {p.Seed} in {MaxAttempts} tries (last: {last}).");
    }

    /// <summary>Development view of layout 2: the first seed only, with no retry; <paramref name="failure"/> names what the checks reject (null = it would pass).</summary>
    public static (MapData Map, MapMeta Meta) GenerateZonedRaw(MapGenParams p, out string? failure)
    {
        ArgumentNullException.ThrowIfNull(p);
        BiomeParams biome = BiomeCatalog.For(p.Biome);
        if (biome.Key != "rust_belt") throw new InvalidOperationException($"Layout 2 is not built yet for {biome.Key}; only rust_belt has the zone skeleton.");
        int n = CellsFor(p.Size);
        var builder = new ProceduralBuilder(biome, n, p.Players, p.Seed);
        CellCoord[] orbit = builder.OrbitStarts;
        MapData full = builder.Build(p.Name ?? DefaultName(p), orbit);
        var meta = new MapMeta(biome.Key, MapSymmetry.MirrorDiag, orbit.Length, p with { Name = null }, builder.Features.ToArray());
        failure = FirstFailure(full, meta) ?? LayoutChecks.FirstFailure(LayoutChecks.Evaluate(full, meta));
        return (full, meta);
    }

    private static (MapData Map, MapMeta Meta) GenerateZoned(MapGenParams p, BiomeParams biome, out int attempts)
    {
        if (biome.Key != "rust_belt") throw new InvalidOperationException($"Layout 2 is not built yet for {biome.Key}; only rust_belt has the zone skeleton.");
        int n = CellsFor(p.Size);
        string name = p.Name ?? DefaultName(p);
        string? last = null;
        // The first attempt that passes every layout check wins. When none does in MaxZonedAttempts, the attempt with the
        // fewest failed layout checks (and a clean validator) is used, so a map is always produced; `map report` lists what it misses.
        MapData? bestMap = null; ProceduralBuilder? bestBuilder = null; int bestFails = int.MaxValue, bestAttempt = 0;
        for (int attempt = 0; attempt < MaxZonedAttempts; attempt++)
        {
            ulong seed = p.Seed + (ulong)attempt;
            var builder = new ProceduralBuilder(biome, n, p.Players, seed);
            CellCoord[] orbit = builder.OrbitStarts;
            MapData full = builder.Build(name, orbit);
            var fullMeta = new MapMeta(biome.Key, MapSymmetry.MirrorDiag, orbit.Length, null, builder.Features.ToArray());
            last = FirstFailure(full, fullMeta);
            if (last is not null) continue;
            int fails = LayoutChecks.Evaluate(full, fullMeta).Count(c => !c.Pass);
            if (fails < bestFails) { bestFails = fails; bestMap = full; bestBuilder = builder; bestAttempt = attempt; }
            if (fails == 0) break;
        }
        if (bestMap is null || bestBuilder is null)
            throw new InvalidOperationException($"No valid {biome.Key} layout-2 map for size {n}, {p.Players} players from seed {p.Seed} in {MaxZonedAttempts} tries (last: {last}).");
        CellCoord[] orb = bestBuilder.OrbitStarts;
        CellCoord[] starts = orb;
        if (p.Players == 3)
        {
            int empty = (int)(p.Seed % 4UL);
            starts = new CellCoord[3];
            for (int k = 0; k < 3; k++) starts[k] = orb[(empty + 1 + k) % 4];
        }
        var map = new MapData(name, bestMap.Theater, n, n, bestMap.Template.ToArray(), bestMap.Land.ToArray(), bestMap.HeightLevel.ToArray(),
            bestMap.Overlay.ToArray(), bestMap.Density.ToArray(), starts);
        var meta = new MapMeta(biome.Key, MapSymmetry.MirrorDiag, p.Players, p with { Name = null }, bestBuilder.Features.ToArray());
        attempts = bestAttempt + 1;
        FailedChecks = bestFails;
        return (map, meta);
    }

    /// <summary>Layout checks the last layout-2 generation on this thread still failed (0 = every check passed); for the sweep test and the report.</summary>
    [ThreadStatic] public static int FailedChecks;

    /// <summary>The first §1 check an attempt fails (validator errors and warnings, then exact path equality), or null.</summary>
    private static string? FirstFailure(MapData map, MapMeta meta)
    {
        foreach (MapIssue issue in MapValidator.Validate(map, meta, null))
            return issue.Code + ": " + issue.Message;
        PathSpread spread = MapAnalysis.Spread(map, map.StartPositions, null, MapAnalysis.FeatureMask(map, meta.Features));
        return spread.IsExact ? null : $"path_equality: {spread}";
    }

    /// <summary>Generates a map. When <paramref name="name"/> is null the name is "synthetic_&lt;size&gt;_&lt;seed&gt;".</summary>
    public static MapData Generate(int size, ulong seed, string? name = null)
    {
        if (size < MinSize || size > SimConstants.MaxMapCells)
            throw new ArgumentOutOfRangeException(nameof(size), $"Size must be {MinSize}..{SimConstants.MaxMapCells}.");
        name ??= $"synthetic_{size}_{seed}";
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            MapData map = Build(size, seed + (ulong)attempt, name);
            if (IsConnected(map, Locomotion.Tracked)) return map;
        }
        throw new InvalidOperationException($"No connected map found for size {size} from seed {seed} in {MaxAttempts} tries.");
    }

    /// <summary>
    /// 4-way flood fill from the first start position; true if every start position is reached.
    /// Uses the generator's own conservative ground rule (Water, Rock and Cliff block every ground
    /// locomotion; Hover crosses Water; Air crosses everything) so map generation never depends on
    /// loaded rules data. Pass a TerrainTable to check against real rules instead.
    /// </summary>
    public static bool IsConnected(MapData map, Locomotion loco, TerrainTable? table = null)
    {
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        var stack = new Stack<int>();
        CellCoord s0 = map.StartPositions[0];
        seen[map.IndexOf(s0)] = true;
        stack.Push(map.IndexOf(s0));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % w, y = i / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int j = ny * w + nx;
                if (seen[j] || !Passable(map.Land[j], loco, table)) continue;
                seen[j] = true;
                stack.Push(j);
            }
        }
        for (int k = 1; k < map.StartPositions.Count; k++)
            if (!seen[map.IndexOf(map.StartPositions[k])]) return false;
        return true;
    }

    private static bool Passable(LandType land, Locomotion loco, TerrainTable? table)
    {
        if (table != null) return table.SpeedPercent(land, loco) > 0;
        if (loco == Locomotion.Air) return true;
        if (land == LandType.Water) return loco == Locomotion.Hover;
        return land != LandType.Rock && land != LandType.Cliff;
    }

    private sealed class Grid
    {
        public readonly int N;
        public readonly LandType[] Land;
        public readonly byte[] Height;
        public readonly OverlayKind[] Overlay;
        public readonly byte[] Density;
        public readonly bool[] Reserved;

        public Grid(int n)
        {
            N = n;
            Land = new LandType[n * n]; Height = new byte[n * n]; Overlay = new OverlayKind[n * n];
            Density = new byte[n * n]; Reserved = new bool[n * n];
        }

        public int Mirror(int i) => N * N - 1 - i;
        public bool Free(int i) => Land[i] == LandType.Clear && Overlay[i] == OverlayKind.None && !Reserved[i];

        /// <summary>Paints a cell and its mirror if both are free (or both are plain clear when ignoring reservations).</summary>
        public bool Paint(int x, int y, LandType land, OverlayKind overlay, byte density, bool ignoreReserved = false)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return false;
            int i = y * N + x, m = Mirror(i);
            if (!(ignoreReserved ? Plain(i) && Plain(m) : Free(i) && Free(m))) return false;
            Land[i] = land; Overlay[i] = overlay; Density[i] = density;
            Land[m] = land; Overlay[m] = overlay; Density[m] = density;
            return true;
        }

        private bool Plain(int i) => Land[i] == LandType.Clear && Overlay[i] == OverlayKind.None;
    }

    private static MapData Build(int n, ulong seed, string name)
    {
        var rng = new SimRandom(seed);
        var g = new Grid(n);

        // Central plateau (height 1) with a one-cell cliff ring and two rotationally paired ramps.
        int lo = n / 4, hi = n - 1 - lo;
        for (int y = lo; y <= hi; y++)
            for (int x = lo; x <= hi; x++)
            {
                int i = y * n + x;
                g.Height[i] = 1;
                if (x == lo || x == hi || y == lo || y == hi) { g.Land[i] = LandType.Cliff; g.Reserved[i] = true; }
            }
        int ry = lo + n / 8;
        for (int k = 0; k < 3; k++)
        {
            int a = (ry + k) * n + lo, b = g.Mirror(a);
            g.Land[a] = LandType.Clear; g.Land[b] = LandType.Clear;
        }
        for (int k = -2; k <= 4; k++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int yy = ry + k, xx = lo + dx;
                if (yy < 0 || xx < 0 || yy >= n || xx >= n) continue;
                g.Reserved[yy * n + xx] = true;
                g.Reserved[g.Mirror(yy * n + xx)] = true;
            }

        // Starts: opposite corners, clear ground, kept free of features.
        var starts = new[] { new CellCoord(StartInset, StartInset), new CellCoord(n - 1 - StartInset, n - 1 - StartInset) };
        foreach (CellCoord s in starts)
            for (int dy = -StartKeepRadius; dy <= StartKeepRadius; dy++)
                for (int dx = -StartKeepRadius; dx <= StartKeepRadius; dx++)
                {
                    int x = s.X + dx, y = s.Y + dy;
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    g.Reserved[y * n + x] = true;
                }

        // Pitchblende: one 2x2 pit near each start (mirror gives the second), two contested near the middle.
        Pit(g, rng, StartInset + 4, StartInset + 1);
        Pit(g, rng, n / 2 - 4, n / 2 - 2);

        // Ashfall fields: 3..5 per half, small blobs on Ash land.
        int ashFields = rng.Next(3, 6);
        for (int f = 0; f < ashFields; f++)
            Blob(g, rng, 1 + rng.Next(2), LandType.Ash, OverlayKind.Ashfall, true);
        // Water pond: one per half.
        Blob(g, rng, 2, LandType.Water, OverlayKind.None, false);
        // Rock outcrops: 2..3 per half.
        int rocks = rng.Next(2, 4);
        for (int f = 0; f < rocks; f++) Blob(g, rng, rng.Next(0, 2), LandType.Rock, OverlayKind.None, false);
        // Rough patches: 3..4 per half.
        int roughs = rng.Next(3, 5);
        for (int f = 0; f < roughs; f++) Blob(g, rng, 2, LandType.Rough, OverlayKind.None, false);

        // Final guarantee: clear ground at the start positions themselves.
        foreach (CellCoord s in starts)
            for (int dy = -StartClearRadius; dy <= StartClearRadius; dy++)
                for (int dx = -StartClearRadius; dx <= StartClearRadius; dx++)
                {
                    int i = (s.Y + dy) * n + s.X + dx;
                    g.Land[i] = LandType.Clear; g.Overlay[i] = OverlayKind.None; g.Density[i] = 0;
                }

        return new MapData(name, "rust_belt", n, n, new ushort[n * n], g.Land, g.Height, g.Overlay, g.Density, starts);
    }

    private static void Pit(Grid g, SimRandom rng, int x0, int y0)
    {
        for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                g.Paint(x0 + dx, y0 + dy, LandType.Clear, OverlayKind.Pitchblende, (byte)rng.Next(8, 13), ignoreReserved: true);
    }

    /// <summary>Paints a diamond blob (Manhattan radius r, ragged rim) at a random spot in the first half.</summary>
    private static void Blob(Grid g, SimRandom rng, int r, LandType land, OverlayKind overlay, bool withDensity)
    {
        int n = g.N;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int cx = rng.Next(n), cy = rng.Next(n / 2);
            if (!g.Free(cy * n + cx) || !g.Free(g.Mirror(cy * n + cx))) continue;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int d = System.Math.Abs(dx) + System.Math.Abs(dy);
                    if (d > r) continue;
                    if (d == r && r > 0 && rng.Next(3) == 0) continue;
                    byte density = withDensity ? (byte)rng.Next(3, 13) : (byte)0;
                    g.Paint(cx + dx, cy + dy, land, overlay, density);
                }
            return;
        }
    }
}
