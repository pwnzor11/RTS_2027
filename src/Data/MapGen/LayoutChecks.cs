// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>One line of the layout report: a check, its measured numbers and whether it passes.</summary>
/// <param name="Name">Short check name.</param>
/// <param name="Value">The measured numbers, readable.</param>
/// <param name="Pass">True when the numbers are inside the target range.</param>
/// <param name="Target">The range, readable.</param>
public sealed record LayoutCheck(string Name, string Value, bool Pass, string Target);

/// <summary>
/// The fairness and quality checks of design 16 section 3.4 as changed by D-183 and design 17 (M12-08), measured on a finished map
/// (Tracked 4-way paths over the built-in passability rule; props and ruins block). Distances that the study gives for a 128-cell map are
/// scaled by the main-to-main path P: contested fields 0.44 P to 0.82 P (45 to 80 at P of about 100), natural 18 to 28 scaled by
/// min(1, side/96). Lane widths use the largest empty square that covers a cell. All integer maths; no hashing containers are iterated.
/// </summary>
public static class LayoutChecks
{
    /// <summary>Money per start (density times 1 for Ashfall, 2 for Pitchblende) of layout 1 on a Rust Belt map, by side 64 / 96 / 128 and players 2 / 4 (measured, 20 seeds).</summary>
    public static long MoneyTarget(int side, int players) => side switch
    {
        < 80 => players == 2 ? 330 : 270,
        < 112 => players == 2 ? 560 : 470,
        _ => players == 2 ? 760 : 640,
    };

    /// <summary>Main interior (cells west and east of the start, half height) for a map side, shared with the generator.</summary>
    public static (int West, int East, int Half) MainRect(int side, int players = 2, bool rot2 = false) =>
        players > 2 ? (side >= 96 ? (6, 11, 8) : (6, 9, 7)) : rot2 ? (side >= 112 ? (8, 16, 10) : (7, 13, 9)) : side >= 96 ? (8, 16, 8) : (7, 13, 7);

    private const int Cap = 20;

    /// <summary>Runs every check on <paramref name="map"/> (with its ruin features in <paramref name="meta"/>) and returns the lines.</summary>
    public static IReadOnlyList<LayoutCheck> Evaluate(MapData map, MapMeta? meta)
    {
        ArgumentNullException.ThrowIfNull(map);
        bool rot2 = false;
        if (meta?.Generator is { Layout: MapGenerator.ZonedLayout } g)
        {
            // A finished layout-2 map is turned by its seed's symmetry; the checks run in the canonical frame again.
            (rot2, int t) = LayoutVariant.For(g.Seed, g.Players, map.Width);
            if (t != 0)
            {
                int w = map.Width;
                map = LayoutVariant.Backward(map, t);
                meta = meta with { Features = LayoutVariant.MapFeatures(meta.Features, w, (x, y) => LayoutVariant.Inv(t, w, x, y)) };
            }
        }
        return EvaluateCanonical(map, meta, rot2);
    }

    /// <summary>The checks on a map in the generator's canonical frame (not turned); <paramref name="rot2"/> selects the half-turn skeleton.</summary>
    internal static IReadOnlyList<LayoutCheck> EvaluateCanonical(MapData map, MapMeta? meta, bool rot2)
    {
        int n = map.Width, total = n * n;
        var res = new List<LayoutCheck>();
        bool[]? feat = MapAnalysis.FeatureMask(map, meta?.Features);
        bool[] pass = Passable(map, feat, true);
        IReadOnlyList<CellCoord> st = map.StartPositions;
        int k = st.Count;
        double f = Math.Min(1.0, n / 96.0);

        // ---- starts: inset, clear radius, side -----------------------------------------------
        int minInset = 1000, maxInset = 0;
        foreach (CellCoord s in st)
        {
            int e = Math.Min(Math.Min(s.X, s.Y), Math.Min(n - 1 - s.X, n - 1 - s.Y));
            int pc = e * 100 / n;
            minInset = Math.Min(minInset, pc); maxInset = Math.Max(maxInset, pc);
        }
        res.Add(new LayoutCheck("start inset", $"{minInset}..{maxInset} % of the side", minInset >= 12 && maxInset <= 25, "12..25 %"));
        int clear = 100;
        foreach (CellCoord s in st) clear = Math.Min(clear, ClearRadius(map, s, feat));
        res.Add(new LayoutCheck("start clear radius", $"{clear} cells", clear >= 5, ">= 5 (liked maps 3..11.5)"));

        // ---- paths and sides -------------------------------------------------------------------
        var dist = new int[k][];
        for (int s = 0; s < k; s++) dist[s] = Bfs(map, pass, st[s]);
        var team = new int[k];
        for (int s = 0; s < k; s++) team[s] = k == 2 ? s : st[s].Y > st[s].X ? 0 : 1;
        int P = int.MaxValue, Pfar = 0, manhattan = 1;
        for (int s = 0; s < k; s++)
            for (int t = 0; t < k; t++)
            {
                if (team[s] == team[t]) continue;
                int d = dist[s][map.IndexOf(st[t])];
                if (d < P) { P = d; manhattan = Math.Abs(st[s].X - st[t].X) + Math.Abs(st[s].Y - st[t].Y); }
                if (d != MapAnalysis.Unreachable) Pfar = Math.Max(Pfar, d);
            }
        if (P == int.MaxValue) { res.Add(new LayoutCheck("enemy path", "unreachable", false, "reachable")); return res; }
        res.Add(new LayoutCheck("enemy path / side", $"{F(P * 100 / n)}..{F(Pfar * 100 / n)}", P * 100 / n >= 77 && Pfar * 100 / n <= 120, "0.8..1.2 (nearest enemy; 0.77 allows rounding)"));
        res.Add(new LayoutCheck("path / straight", $"{F(P * 100 / manhattan)}", P * 100 / manhattan >= 100 && P * 100 / manhattan <= 115, "1.0..1.15 (Manhattan, as measured in the study)"));
        if (k >= 3)
        {
            int mates = int.MaxValue;
            for (int s = 0; s < k; s++)
                for (int t = 0; t < k; t++)
                    if (s != t && team[s] == team[t]) mates = Math.Min(mates, dist[s][map.IndexOf(st[t])]);
            res.Add(new LayoutCheck("teammates path", $"{mates} cells", mates >= 14 && mates <= 48, "14..48"));
        }

        // ---- entrances ---------------------------------------------------------------------------
        (int iw, int ie, int hh) = MainRect(n, k, rot2);
        string entr = ""; bool entrOk = true; int minEntr = 1000;
        for (int s = 0; s < k; s++)
        {
            List<int> widths = Entrances(map, pass, st[s], iw, ie, hh, rot2);
            widths.Sort();
            int minApproach = k > 2 ? 4 : 3;   // 3-4 players: a 3-wide nick at a corner of the shell is not counted as an approach
            int wide = 0; foreach (int w in widths) if (w >= minApproach) wide++;
            bool ok = wide >= 2 && wide <= 3;
            if (wide == 1 && widths.Count(w => w >= minApproach) == 1) { int only = widths.First(w => w >= minApproach); ok = only >= 4 && only <= 6; }
            if (wide == 2 && widths.Count(w => w >= 4 && w <= 6) < 2 && widths.Max() > 9 ) ok = true;
            entrOk &= ok;
            if (s == 0) entr = string.Join("+", widths.Where(w => w >= minApproach));
            foreach (int w in widths) if (w >= minApproach) minEntr = Math.Min(minEntr, w);
        }
        res.Add(new LayoutCheck("main entrances", $"start 1: {entr} wide; narrowest {minEntr}", entrOk && minEntr >= 3, "2-3 approaches >= 3 wide (soft) or 1 ramp / 2 gates of 4..6 (walled)"));

        // ---- lanes -----------------------------------------------------------------------------
        int[] width = Widths(pass, n);
        string laneText = ""; bool laneOk = true;
        var pairs = new List<(int, int)>();
        for (int t = 0; t < k; t++) if (team[t] != team[0]) pairs.Add((0, t));
        if (k == 4 && pairs.Count > 1) { int nearest = pairs.OrderBy(q => dist[q.Item1][map.IndexOf(st[q.Item2])]).First().Item2; pairs = pairs.Where(q => q.Item2 == nearest).ToList(); }   // the lanes that matter are those to the nearest enemy
        int lanesMin = 99, lanesMax = 0, widest = 0, narrowMin = 99; bool hasMedium = true, hasWide = true, hasNarrow = true; int maxShare = 0;
        int gapsMax = 0; bool anyMedium = false, anyNarrow = false;
        foreach ((int s, int t) in pairs)
        {
            List<Lane> found = FindLanes(map, pass, width, st[s], st[t], n, dist[s], dist[t], f, dist[s][map.IndexOf(st[t])], rot2);
            List<Lane> lanes = found.Where(l => l.Min >= 3).ToList();   // a route squeezing through a 1-2 wide gap is an infantry gap, not a lane
            gapsMax = Math.Max(gapsMax, found.Count - lanes.Count);
            lanesMin = Math.Min(lanesMin, lanes.Count); lanesMax = Math.Max(lanesMax, lanes.Count);
            int wMax = 0; bool med = false, narrow = false;
            foreach (Lane l in lanes)
            {
                wMax = Math.Max(wMax, l.Median); narrowMin = Math.Min(narrowMin, l.Min);
                if (l.Median >= 5 && l.Median <= 9) med = true;
                if (l.Min >= 3 && l.Min <= 5) narrow = true;
                maxShare = Math.Max(maxShare, l.SharePercent);
            }
            widest = Math.Max(widest, wMax);
            hasWide &= wMax >= (int)(12 * f);
            if (k == 3) { anyMedium |= med; anyNarrow |= narrow; } else { hasMedium &= med; hasNarrow &= narrow; }   // 3 players: the lane mix may come from either enemy
            laneText += (laneText.Length > 0 ? " | " : "") + $"{st[s].X},{st[s].Y}->{st[t].X},{st[t].Y}: " +
                string.Join(", ", found.Select(l => $"[len {l.Length} median {l.Median} min {l.Min} at {l.MinAt % n},{l.MinAt / n}]"));
            if (lanes.Count == 0) laneOk = false;
        }
        if (k == 3) { hasMedium = anyMedium; hasNarrow = anyNarrow; }
        res.Add(new LayoutCheck("lane count", $"{lanesMin}..{lanesMax} lanes of width >= 3 per enemy pair, {gapsMax} infantry gap(s) of 1-2; {laneText}", laneOk && (k == 3 ? lanesMax >= 2 : lanesMin >= 2) && lanesMax <= 5, "2..5, about 3"));
        res.Add(new LayoutCheck("lane share", $"{maxShare} % shared beyond the starts' surroundings", maxShare < 30, "< 30 %"));
        res.Add(new LayoutCheck("wide lane", $"widest median {widest}", hasWide, $"one lane with median width >= {(int)(12 * f)} (12..16 at Medium and up; the cap is 20)"));
        res.Add(new LayoutCheck("medium lane", hasMedium ? "present" : "missing", hasMedium, "one lane with median width 5..9"));
        res.Add(new LayoutCheck("narrow choke", $"narrowest lane point {narrowMin}", hasNarrow, "one lane whose narrowest point is 3..5"));
        res.Add(new LayoutCheck("lane >= 3 wide", $"narrowest point of the best lane {BestMin(map, pass, width, st, team, n, dist, f)}", BestMin(map, pass, width, st, team, n, dist, f) >= 3, ">= 3"));

        // ---- fields ----------------------------------------------------------------------------------
        int[] fid = MapAnalysis.FieldIds(map, out int fc);
        var size = new int[fc]; var money = new int[fc]; var pitch = new bool[fc];
        for (int i = 0; i < total; i++)
        {
            int id = fid[i]; if (id < 0) continue;
            size[id]++; bool p = map.Overlay[i] == OverlayKind.Pitchblende; pitch[id] |= p;
            money[id] += map.Density[i] * (p ? 2 : 1);
        }
        var near = new int[k][]; // per start: nearest-cell distance to each field
        for (int s = 0; s < k; s++)
        {
            near[s] = new int[fc]; Array.Fill(near[s], MapAnalysis.Unreachable);
            for (int i = 0; i < total; i++) if (fid[i] >= 0 && dist[s][i] < near[s][fid[i]]) near[s][fid[i]] = dist[s][i];
        }
        int radius = (int)(30 * f);
        var cells30 = new long[k]; var money30 = new long[k];
        for (int s = 0; s < k; s++)
            for (int i = 0; i < total; i++)
                if (fid[i] >= 0 && dist[s][i] <= radius)
                {
                    cells30[s]++; money30[s] += map.Density[i] * (map.Overlay[i] == OverlayKind.Pitchblende ? 2 : 1);
                }
        long cMin = cells30.Min(), cMax = cells30.Max();
        res.Add(new LayoutCheck("resource per start", $"{cMin}..{cMax} cells within {radius} path cells; money {money30.Min()}..{money30.Max()}", cMin >= (int)(100 * f) && cMax <= 220, $"{(int)(100 * f)}..220 cells (the lower bound scales with min(1, side/96))"));
        res.Add(new LayoutCheck("resource parity", $"cells {cMin}/{cMax}, money {money30.Min()}/{money30.Max()}", cMin == cMax && money30.Min() == money30.Max(), "equal across starts"));
        long allMoney = 0; for (int i = 0; i < fc; i++) allMoney += money[i];
        res.Add(new LayoutCheck("money per start", $"{allMoney / k} (layout 1 target {MoneyTarget(n, k == 2 ? 2 : 4)})", true, "reported"));
        int gem = 0, rc = 0; for (int i = 0; i < total; i++) if (fid[i] >= 0) { rc++; if (map.Overlay[i] == OverlayKind.Pitchblende) gem++; }
        res.Add(new LayoutCheck("gem share", $"{gem * 100 / Math.Max(1, rc)} %", gem * 100 <= 15 * rc, "<= 15 %"));

        // natural
        int lo = (int)(18 * f), hi = (int)(28 * f) + 1;
        bool natAny = false, natAll = true; string natText = "none";
        for (int s = 0; s < k; s++)
        {
            bool has = false;
            for (int q = 0; q < fc; q++)
            {
                if (size[q] < 30 || size[q] > 80 || near[s][q] < lo || near[s][q] > hi) continue;
                int enemy = int.MaxValue;
                for (int t = 0; t < k; t++) if (team[t] != team[s]) enemy = Math.Min(enemy, near[t][q]);
                if (enemy * 10 < near[s][q] * 25) continue;
                has = true; natText = $"{size[q]} cells at {near[s][q]}, enemy {F(enemy * 100 / near[s][q])}x as far";
            }
            natAny |= has; natAll &= has;
        }
        res.Add(new LayoutCheck("natural", natAny ? natText : "none on this seed", natAny == natAll, "none, or 30..80 cells at 18..28, enemy >= 2.5x (all starts alike)"));

        int contested = 0, rich = 0; string ctext = "", all = "";
        var firstCell = new int[fc]; Array.Fill(firstCell, -1);
        for (int i = 0; i < total; i++) if (fid[i] >= 0 && firstCell[fid[i]] < 0) firstCell[fid[i]] = i;
        for (int q = 0; q < fc; q++) all += $" ({firstCell[q] % n},{firstCell[q] / n}){size[q]}@{string.Join("/", Enumerable.Range(0, k).Select(s => near[s][q]))}";
        for (int q = 0; q < fc; q++)
        {
            bool ok = size[q] >= 20 && size[q] <= 70;
            for (int s = 0; s < k && ok; s++) ok = near[s][q] >= P * (k > 2 ? 40 : 44) / 100 && near[s][q] <= P * (k > 2 ? 90 : 88) / 100;
            if (!ok) continue;
            contested++;
            if (pitch[q])
            {
                int lo2 = int.MaxValue, hi2 = 0;
                for (int s = 0; s < k; s++) { lo2 = Math.Min(lo2, near[s][q]); hi2 = Math.Max(hi2, near[s][q]); }
                if (hi2 * 10 <= lo2 * 13) rich++;   // a rich CENTRE field (about equally far from every start); an empty 3-player main is not one
            }
            ctext += (ctext.Length > 0 ? ", " : "") + size[q];
        }
        // Fields come once per symmetry image; the study counts them per map.
        res.Add(new LayoutCheck("contested fields", $"{contested} ({ctext} cells), rich {rich}; all fields (cells@path from each start):{all}", contested >= (k > 2 ? 1 : 2) && contested <= 6 && rich <= 1, "2..6 of 20..70 cells at 0.44..0.88 P from every start, at most one rich centre (3-4 players: 1..6 at 0.40..0.90 P, only the centre is about as far from all starts)"));

        // ---- terrain --------------------------------------------------------------------------------
        int rock = 0, water = 0, props = 0, road = 0;
        for (int i = 0; i < total; i++)
        {
            LandType l = map.Land[i];
            if (l is LandType.Rock or LandType.Cliff) rock++; else if (l == LandType.Water) water++; else if (l == LandType.Road) road++;
            if (OverlayKinds.IsProp(map.Overlay[i])) props++;
        }
        int blocked = rock + water + props;
        res.Add(new LayoutCheck("obstacles", $"{F(blocked * 10000 / total)} % (rock {F(rock * 10000 / total)}, water {F(water * 10000 / total)}, props {F(props * 10000 / total)})",
            blocked * 100 >= 10 * total && blocked * 100 <= 19 * total, "10..19 % (rock 5..12, props 1..4, water 0..5)"));
        res.Add(new LayoutCheck("roads", $"{F(road * 10000 / total)} %", road * 10 <= total, "<= 10 %"));
        res.Add(new LayoutCheck("empty area", $"{F(EmptyPermille(map, feat) * 10)} % of cells centre an empty 15x15 window", EmptyPermille(map, feat) <= 30, "<= 3 %"));

        // ---- prop safety ----------------------------------------------------------------------------
        bool reach = true;
        bool[] noProps = Passable(map, feat, false);
        for (int s = 0; s < k; s++)
        {
            int[] d0 = Bfs(map, noProps, st[s]);
            for (int i = 0; i < total; i++)
                if (d0[i] != MapAnalysis.Unreachable && dist[s][i] == MapAnalysis.Unreachable && fid[i] >= 0) reach = false;
            for (int t = 0; t < k; t++) if (dist[s][map.IndexOf(st[t])] == MapAnalysis.Unreachable) reach = false;
        }
        List<Lane> without = pairs.Count == 0 ? new List<Lane>() : FindLanes(map, noProps, Widths(noProps, n), st[pairs[0].Item1], st[pairs[0].Item2], n, Bfs(map, noProps, st[pairs[0].Item1]), Bfs(map, noProps, st[pairs[0].Item2]), f, P);
        List<Lane> with = pairs.Count == 0 ? new List<Lane>() : FindLanes(map, pass, width, st[pairs[0].Item1], st[pairs[0].Item2], n, dist[pairs[0].Item1], dist[pairs[0].Item2], f, P);
        bool propSafe = reach && with.Count >= Math.Min(without.Count, 2);
        res.Add(new LayoutCheck("prop safety", $"lanes {with.Count} with props, {without.Count} without; every field and start reachable: {reach}", propSafe, "props never cut a lane below 2 or a field off"));
        return res;
    }

    /// <summary>True when every check passes.</summary>
    public static bool AllPass(IReadOnlyList<LayoutCheck> checks) => checks.All(c => c.Pass);

    /// <summary>The names of the failing checks, joined, or null.</summary>
    public static string? FirstFailure(IReadOnlyList<LayoutCheck> checks)
    {
        var bad = checks.Where(c => !c.Pass).Select(c => c.Name).ToList();
        return bad.Count == 0 ? null : string.Join(", ", bad);
    }

    private static string F(int hundredths) => (hundredths / 100.0).ToString("0.##", CultureInfo.InvariantCulture);

    // ---- helpers ------------------------------------------------------------------------------------

    private static bool[] Passable(MapData map, bool[]? feat, bool propsBlock)
    {
        int total = map.Width * map.Height;
        var p = new bool[total];
        for (int i = 0; i < total; i++)
            p[i] = MapAnalysis.BuiltInPassable(map.Land[i], Locomotion.Tracked)
                && !(propsBlock && OverlayKinds.BlocksGround(map.Overlay[i])) && !(feat is not null && feat[i]);
        return p;
    }

    private static int[] Bfs(MapData map, bool[] pass, CellCoord from)
    {
        int w = map.Width, total = w * map.Height;
        var d = new int[total]; Array.Fill(d, MapAnalysis.Unreachable);
        var q = new int[total]; int head = 0, tail = 0;
        int s = map.IndexOf(from); d[s] = 0; q[tail++] = s;
        while (head < tail)
        {
            int i = q[head++]; int x = i % w, y = i / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= map.Height) continue;
                int j = ny * w + nx;
                if (d[j] != MapAnalysis.Unreachable || !pass[j]) continue;
                d[j] = d[i] + 1; q[tail++] = j;
            }
        }
        return d;
    }

    private static int ClearRadius(MapData map, CellCoord s, bool[]? feat)
    {
        for (int r = 1; r <= 14; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(s.X + dx, s.Y + dy);
                    if (!map.Contains(c)) return r - 1;
                    int i = map.IndexOf(c);
                    if (map.Land[i] is LandType.Rock or LandType.Water or LandType.Cliff || OverlayKinds.IsProp(map.Overlay[i]) || (feat is not null && feat[i])) return r - 1;
                }
        return 14;
    }

    private static (int X, int Y) Img(int k, int n, int x, int y) => k switch
    {
        0 => (x, y), 1 => (y, x), 2 => (n - 1 - y, n - 1 - x), _ => (n - 1 - x, n - 1 - y),
    };

    /// <summary>Widths of the passable runs on the square ring 2 cells outside the main interior, in the frame of the start's
    /// upper-left-type image (the interior sits at x -iw..+ie, y -hh..+hh around the start).</summary>
    private static List<int> Entrances(MapData map, bool[] pass, CellCoord s, int iw, int ie, int hh, bool rot2)
    {
        int n = map.Width;
        // Find which image of the start is the frame start: x < y and x + y < n - 1.
        int frame = 0;
        if (rot2) { foreach (int k in new[] { 0, 3 }) { (int x, int y) = Img(k, n, s.X, s.Y); if (2 * x < n - 1) { frame = k; break; } } }
        else for (int k = 0; k < 4; k++) { (int x, int y) = Img(k, n, s.X, s.Y); if (x < y && x + y < n - 1) { frame = k; break; } }
        (int fx, int fy) = Img(frame, n, s.X, s.Y);   // the images are involutions, so Img(frame, .) maps back too
        var ring = new List<(int X, int Y)>();
        int x0 = fx - iw - 2, x1 = fx + ie + 2, y0 = fy - hh - 2, y1 = fy + hh + 2;
        for (int x = x0; x <= x1; x++) ring.Add((x, y0));
        for (int y = y0 + 1; y <= y1; y++) ring.Add((x1, y));
        for (int x = x1 - 1; x >= x0; x--) ring.Add((x, y1));
        for (int y = y1 - 1; y > y0; y--) ring.Add((x0, y));
        var open = new List<bool>();
        foreach ((int x, int y) in ring)
        {
            (int ax, int ay) = Img(frame, n, x, y);
            open.Add(ax >= 0 && ay >= 0 && ax < n && ay < n && pass[ay * n + ax]);
        }
        var runs = new List<int>();
        int m = open.Count, startAt = open.IndexOf(false);
        if (startAt < 0) { runs.Add(m); return runs; }
        int cur = 0;
        for (int q = 1; q <= m; q++)
        {
            if (open[(startAt + q) % m]) cur++;
            else { if (cur > 0) runs.Add(cur); cur = 0; }
        }
        return runs;
    }

    /// <summary>Side of the largest empty square that covers each passable cell (capped), 0 for blocked cells.</summary>
    private static int[] Widths(bool[] pass, int n)
    {
        var dp = new int[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (!pass[i]) continue;
                int a = x > 0 ? dp[i - 1] : 0, b = y > 0 ? dp[i - n] : 0, c = x > 0 && y > 0 ? dp[i - n - 1] : 0;
                dp[i] = Math.Min(Cap, Math.Min(a, Math.Min(b, c)) + 1);
            }
        var w = new int[n * n];
        for (int s = Cap; s >= 1; s--)
            for (int y = s - 1; y < n; y++)
                for (int x = s - 1; x < n; x++)
                {
                    if (dp[y * n + x] < s) continue;
                    for (int yy = y - s + 1; yy <= y; yy++)
                        for (int xx = x - s + 1; xx <= x; xx++)
                            if (w[yy * n + xx] < s) w[yy * n + xx] = s;
                }
        return w;
    }

    private sealed record Lane(int Length, int Median, int Min, int SharePercent, int MinAt);

    private const int Exclude = 20;
    private const int ExcludeBlock = 44;   // lanes may share this much of the way out of a main (the service street of a walled main)

    private static List<Lane> FindLanes(MapData map, bool[] pass, int[] width, CellCoord a, CellCoord b, int n, int[] da, int[] db, double f, int pathLen, bool rot2Lanes = false)
    {
        var lanes = new List<Lane>();
        int ex = Math.Min((int)(Exclude * f), pathLen * 22 / 100), exb = Math.Min((int)(ExcludeBlock * f), pathLen * 38 / 100);
        var blockedLane = new bool[n * n];
        var used = new bool[n * n];
        int first = 0;
        for (int round = 0; round < 8; round++)
        {
            List<int>? path = Route(pass, width, blockedLane, map.IndexOf(a), map.IndexOf(b), n);
            if (path is null)
            {
                break;
            }
            if (round == 0) first = path.Count;
            else if (path.Count > first * 14 / 10) break;
            var inner = new List<int>(); int minAt = path[0], minW = 1000;
            int shared = 0, counted = 0;
            foreach (int c in path)
            {
                bool near = da[c] <= ex || db[c] <= ex;
                if (near) continue;
                inner.Add(width[c]); counted++; if (width[c] < minW) { minW = width[c]; minAt = c; }
                if (used[c]) shared++;
            }
            if (inner.Count == 0) break;
            inner.Sort();
            lanes.Add(new Lane(path.Count, inner[inner.Count / 2], inner[0], counted == 0 ? 0 : shared * 100 / counted, minAt));
            foreach (int c in path)
            {
                int x = c % n, y = c / n;
                bool near = da[c] <= exb || db[c] <= exb;
                used[c] = true;
                if (near) continue;
                int r = Math.Min(rot2Lanes ? 7 : 12, (width[c] + 1) / 2 + (rot2Lanes ? 1 : 2));
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (x + dx >= 0 && y + dy >= 0 && x + dx < n && y + dy < n) blockedLane[(y + dy) * n + x + dx] = true;
            }
        }
        return lanes;
    }

    private static int BestMin(MapData map, bool[] pass, int[] width, IReadOnlyList<CellCoord> st, int[] team, int n, int[][] dist, double f)
    {
        int best = 0;
        for (int t = 0; t < st.Count; t++)
        {
            if (team[t] == team[0]) continue;
            foreach (Lane l in FindLanes(map, pass, width, st[0], st[t], n, dist[0], dist[t], f, dist[0][map.IndexOf(st[t])])) best = Math.Max(best, l.Min);
            break;
        }
        return best;
    }

    /// <summary>Cheapest route (4-way) that prefers open ground; cells in <paramref name="blockedLane"/> are closed except near the ends.</summary>
    private static List<int>? Route(bool[] pass, int[] width, bool[] blockedLane, int from, int to, int n)
    {
        int total = n * n;
        var cost = new int[total]; Array.Fill(cost, int.MaxValue);
        var prev = new int[total]; Array.Fill(prev, -1);
        var pq = new PriorityQueue<int, int>();
        cost[from] = 0; pq.Enqueue(from, 0);
        while (pq.Count > 0)
        {
            pq.TryDequeue(out int i, out int ci);
            if (ci > cost[i]) continue;
            if (i == to) break;
            int x = i % n, y = i / n;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                int j = ny * n + nx;
                if (!pass[j] || blockedLane[j]) continue;
                int w = width[j];
                int step = 10 + (w < 3 ? 30 : w < 5 ? 8 : w < 8 ? 3 : 0);
                if (ci + step < cost[j]) { cost[j] = ci + step; prev[j] = i; pq.Enqueue(j, cost[j]); }
            }
        }
        if (prev[to] < 0 && from != to) return null;
        var path = new List<int>();
        for (int c = to; c >= 0; c = prev[c]) { path.Add(c); if (c == from) break; }
        path.Reverse();
        return path;
    }

    /// <summary>Per mille of cells whose 15x15 window holds nothing: no prop, ruin, resource, height change or land other than the commonest.</summary>
    private static int EmptyPermille(MapData map, bool[]? feat)
    {
        int n = map.Width;
        LandType baseLand = LandType.Clear;
        var sum = new int[(n + 1) * (n + 1)];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                bool something = map.Land[i] != baseLand || map.Overlay[i] != OverlayKind.None || map.HeightLevel[i] != 0 || (feat is not null && feat[i]);
                sum[(y + 1) * (n + 1) + x + 1] = sum[y * (n + 1) + x + 1] + sum[(y + 1) * (n + 1) + x] - sum[y * (n + 1) + x] + (something ? 1 : 0);
            }
        int empty = 0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int x0 = Math.Max(0, x - 7), y0 = Math.Max(0, y - 7), x1 = Math.Min(n, x + 8), y1 = Math.Min(n, y + 8);
                if (sum[y1 * (n + 1) + x1] - sum[y0 * (n + 1) + x1] - sum[y1 * (n + 1) + x0] + sum[y0 * (n + 1) + x0] == 0) empty++;
            }
        return empty * 1000 / (n * n);
    }
}
