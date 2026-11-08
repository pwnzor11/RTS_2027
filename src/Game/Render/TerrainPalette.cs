// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Render;

/// <summary>
/// The terrain palette of a theater, read from <c>data/theaters/&lt;theater&gt;.toml</c> (the shipped look, variant C): three shades per land type, the
/// ashfall and pitchblende overlay colours and the texture settings. The game bakes it per map (see
/// <see cref="TerrainBake"/>); <c>tools/maps/make_rustbelt_m2.py</c> reads the same file for the proof images.
/// </summary>
public sealed class TerrainPalette
{
    /// <summary>The map header <c>theater</c> value that selects this palette. Other theaters keep the debug look.</summary>
    public const string RustBeltTheater = "rustbelt";

    /// <summary>The only palette file layout this build reads.</summary>
    public const int SupportedFormatVersion = 1;

    private readonly LandShades[] _land;

    private TerrainPalette(int texelCells, LandShades[] land, Rgb ashfallSpeck, int ashfallMaxPermille,
        Rgb pitchChunk, Rgb pitchGlint, int pitchMaxPermille, int glintOneIn, TerrainStyle style,
        Rgb? minimapAshfall, Rgb? minimapPitchblende)
    {
        Style = style;
        MinimapAshfall = minimapAshfall;
        MinimapPitchblende = minimapPitchblende;
        TexelCells = texelCells;
        _land = land;
        AshfallSpeck = ashfallSpeck;
        AshfallMaxPermille = ashfallMaxPermille;
        PitchChunk = pitchChunk;
        PitchGlint = pitchGlint;
        PitchMaxPermille = pitchMaxPermille;
        GlintOneIn = glintOneIn;
    }

    /// <summary>Texels per map cell in the baked texture.</summary>
    public int TexelCells { get; }

    /// <summary>Decals and land-linked sprites for this theater (<c>[decor]</c> section); <see cref="DecorTable.Empty"/> when the file has none.</summary>
    public DecorTable Decor { get; private set; } = DecorTable.Empty;

    /// <summary>The mood of the theater (<c>[mood]</c> table, D-184): world colour grade and dust colour; <see cref="MoodGrade.None"/> when the file has none.</summary>
    public MoodGrade Mood { get; private set; } = MoodGrade.None;

    /// <summary>Optional edge, tone and resource-field options (S15 looks). <see cref="TerrainStyle.None"/> = the plain look.</summary>
    public TerrainStyle Style { get; }

    /// <summary>
    /// A cell's density on the 0..255 scale the look rules use. The sim's real scale is 0..<see cref="TerrainStyle.DensityFull"/> (12 for
    /// generated maps), so a palette sets <c>density_full</c> to make a rich cell look rich; the default 255 leaves values unchanged.
    /// </summary>
    public int NormalDensity(int density) => Math.Min(255, density * 255 / Style.DensityFull);

    /// <summary>Minimap colour of an ashfall cell at full density, or null for the old tint-by-speck-colour rule.</summary>
    public Rgb? MinimapAshfall { get; }

    /// <summary>Minimap colour of a pitchblende cell at full density, or null for the old tint-by-chunk-colour rule.</summary>
    public Rgb? MinimapPitchblende { get; }

    /// <summary>
    /// The look variant folder to read palettes from: the <c>RTS_TERRAIN_LOOK</c> environment variable (<c>classic</c>, <c>a</c>, <c>b</c>
    /// or <c>c</c>), or null for the shipped look (D-181: <c>data/theaters/&lt;theater&gt;.toml</c>, a copy of variant C). A variant file
    /// lives at <c>data/theaters/variants/&lt;look&gt;/&lt;theater&gt;.toml</c>; <c>classic</c> is the pre-S15 look kept for comparison. A
    /// missing variant file falls back to the shipped palette. Unset = shipped.
    /// </summary>
    public static string? ActiveLook { get; set; } = LookFromEnvironment();

    private static string? LookFromEnvironment()
    {
        string? v = Environment.GetEnvironmentVariable("RTS_TERRAIN_LOOK");
        if (string.IsNullOrWhiteSpace(v)) return null;
        string n = NormalizeTheaterId(v);
        return n.Length == 0 ? null : n;
    }

    /// <summary>WCAG relative luminance (0..1) of an sRGB colour.</summary>
    public static double Luminance(Rgb c)
    {
        static double Lin(byte v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    /// <summary>WCAG contrast ratio (1..21) between two colours; symmetric.</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Colour of the darker ashfall speckle.</summary>
    public Rgb AshfallSpeck { get; }

    /// <summary>Speckle coverage at density 255, in permille. Coverage rises linearly with density.</summary>
    public int AshfallMaxPermille { get; }

    /// <summary>Colour of pitchblende rock chunks.</summary>
    public Rgb PitchChunk { get; }

    /// <summary>Colour of the bright glints inside pitchblende chunks.</summary>
    public Rgb PitchGlint { get; }

    /// <summary>Chunk coverage at density 255, in permille.</summary>
    public int PitchMaxPermille { get; }

    /// <summary>One texel in this many inside a chunk is a glint.</summary>
    public int GlintOneIn { get; }

    /// <summary>The dark, base and light shades for a land type.</summary>
    public LandShades ShadesFor(LandType land) => _land[(int)land];

    /// <summary>True when a map's theater key selects the Rust Belt look.</summary>
    public static bool IsRustBelt(string theater) => string.Equals(theater, RustBeltTheater, StringComparison.Ordinal);

    /// <summary>
    /// Loads a palette. The argument is a theater id (<c>rustbelt</c>, <c>glass_coast</c>, <c>rust_belt</c>, ...) looked up as
    /// <c>data/theaters/&lt;id&gt;.toml</c>, or a path to a palette file (anything with a directory separator or a <c>.toml</c>
    /// extension). An unknown theater id falls back to the Rust Belt palette and raises <see cref="Warned"/> once per id.
    /// </summary>
    public static TerrainPalette Load(string theaterIdOrPath)
    {
        ArgumentNullException.ThrowIfNull(theaterIdOrPath);
        if (theaterIdOrPath.Contains('/') || theaterIdOrPath.Contains('\\')
            || theaterIdOrPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
            return FromToml(File.ReadAllText(theaterIdOrPath));

        string id = NormalizeTheaterId(theaterIdOrPath);
        string? look = ActiveLook;
        string cacheKey = look is null ? id : look + "/" + id;
        lock (Cache)
        {
            if (Cache.TryGetValue(cacheKey, out TerrainPalette? cached)) return cached;
            string? file = id.Length == 0 ? null : FindTheaterFile(id, look);
            TerrainPalette palette;
            if (file is null)
            {
                if (id != RustBeltTheater) Warn($"unknown theater '{theaterIdOrPath}': using the {RustBeltTheater} palette");
                palette = id == RustBeltTheater ? throw new FileNotFoundException("data/theaters/rustbelt.toml not found") : Load(RustBeltTheater);
            }
            else palette = FromToml(File.ReadAllText(file));
            Cache[cacheKey] = palette;
            return palette;
        }
    }

    /// <summary>Raised (once per unknown theater id) when <see cref="Load"/> falls back to the Rust Belt palette.</summary>
    public static event Action<string>? Warned;

    /// <summary>
    /// Folds a theater or biome key to a palette file name: lower case, letters and digits only, so <c>rust_belt</c> is the
    /// same theater as <c>rustbelt</c>, <c>glass_coast</c> as <c>glasscoast</c>.
    /// </summary>
    public static string NormalizeTheaterId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (c is >= 'A' and <= 'Z') sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>
    /// The theater id a map is drawn with: its biome (when <paramref name="meta"/> names one), otherwise its header theater.
    /// Maps without metadata are Rust Belt unless their header says otherwise.
    /// </summary>
    public static string TheaterIdFor(MapData map, MapMeta? meta)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (meta is not null && !string.IsNullOrEmpty(meta.Biome)) return NormalizeTheaterId(meta.Biome);
        return string.IsNullOrEmpty(map.Theater) ? RustBeltTheater : map.Theater;
    }

    /// <summary>The palette a map is drawn with (see <see cref="TheaterIdFor"/>).</summary>
    public static TerrainPalette ForMap(MapData map, MapMeta? meta = null) => Load(TheaterIdFor(map, meta));

    private static readonly Dictionary<string, TerrainPalette> Cache = new(StringComparer.Ordinal);

    private static void Warn(string message)
    {
        Console.Error.WriteLine("warning: " + message);
        Warned?.Invoke(message);
    }

    private static string? FindTheaterFile(string id, string? look)
    {
        // The shipped files are rustbelt.toml, glass_coast.toml, mountains.toml and ash_plains.toml; ids are folded without underscores.
        string[] names = id switch
        {
            "glasscoast" => new[] { "glasscoast", "glass_coast" },
            "ashplains" => new[] { "ashplains", "ash_plains" },
            _ => new[] { id },
        };
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            foreach (string name in names)
            {
                if (look is not null)
                {
                    string variant = Path.Combine(dir, "data", "theaters", "variants", look, name + ".toml");
                    if (File.Exists(variant)) return variant;
                }

                string candidate = Path.Combine(dir, "data", "theaters", name + ".toml");
                if (File.Exists(candidate)) return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>
    /// Parses palette TOML. Throws <see cref="InvalidDataException"/> when a land type has no colour, a colour is not
    /// <c>#RRGGBB</c>, or a value is missing or out of range.
    /// </summary>
    public static TerrainPalette FromToml(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(text) ?? throw new InvalidDataException("empty palette file");

        int version = Int(root, "format_version");
        if (version != SupportedFormatVersion) throw new InvalidDataException($"unsupported palette format_version {version}");
        int texel = Int(root, "texel_cells");
        if (texel < 1 || texel > 32) throw new InvalidDataException($"texel_cells {texel} out of range 1..32");

        TomlTable land = Table(root, "land");
        var shades = new LandShades[RuleCounts.LandTypes];
        foreach (LandType type in Enum.GetValues<LandType>())
        {
            if (type == LandType.Coast && !land.ContainsKey("coast"))   // M7-02: older palettes have no beach; draw it like clear ground
            {
                shades[(int)type] = shades[(int)LandType.Clear];
                continue;
            }
            TomlTable t = Table(land, type.ToString().ToLowerInvariant());
            shades[(int)type] = new LandShades(Colour(t, "dark"), Colour(t, "base"), Colour(t, "light"));
        }

        TomlTable overlay = Table(root, "overlay");
        TomlTable ash = Table(overlay, "ashfall");
        TomlTable pitch = Table(overlay, "pitchblende");
        int oneIn = Int(pitch, "glint_one_in");
        if (oneIn < 1) throw new InvalidDataException("glint_one_in must be at least 1");

        TerrainStyle style = ParseStyle(root);
        Rgb? miniAsh = null, miniPitch = null;
        if (root.TryGetValue("minimap", out object? mm) && mm is TomlTable mini)
        {
            miniAsh = OptColour(mini, "ashfall");
            miniPitch = OptColour(mini, "pitchblende");
        }

        var palette = new TerrainPalette(texel, shades,
            Colour(ash, "speck"), Permille(ash, "max_coverage_permille"),
            Colour(pitch, "chunk"), Colour(pitch, "glint"), Permille(pitch, "max_coverage_permille"), oneIn,
            style, miniAsh, miniPitch);
        palette.Decor = ParseDecor(root);
        palette.Mood = MoodGrade.Parse(root);
        return palette;
    }

    private static DecorTable ParseDecor(TomlTable root)
    {
        if (!root.TryGetValue("decor", out object? dv) || dv is not TomlTable decor) return DecorTable.Empty;
        var rules = new List<DecalRule>();
        if (decor.TryGetValue("decal", out object? list) && list is System.Collections.IEnumerable items)
        {
            foreach (object? item in items)
            {
                if (item is not TomlTable t) throw new InvalidDataException("[[decor.decal]] entries must be tables");
                string name = t.TryGetValue("name", out object? n) && n is string ns ? ns : "decal";
                string shapeName = t.TryGetValue("shape", out object? sh) && sh is string ss ? ss : "";
                if (!Enum.TryParse(shapeName, true, out DecalShape shape)) throw new InvalidDataException($"decal '{name}': unknown shape '{shapeName}'");
                int mask = 0;
                if (!t.TryGetValue("on", out object? on) || on is not System.Collections.IEnumerable lands || on is string)
                    throw new InvalidDataException($"decal '{name}': 'on' must be a list of land types");
                foreach (object? l in lands)
                {
                    if (l is not string ls || !Enum.TryParse(ls, true, out LandType lt)) throw new InvalidDataException($"decal '{name}': unknown land type '{l}'");
                    mask |= 1 << (int)lt;
                }

                int sizeMin = OptInt(t, "size_min", 8, 1, 64), sizeMax = OptInt(t, "size_max", sizeMin, sizeMin, 64);
                rules.Add(new DecalRule(name, shape, mask, Permille(t, "permille"), Colour(t, "colour"), OptInt(t, "alpha", 255, 1, 255), sizeMin, sizeMax));
            }
        }

        return new DecorTable(rules, OptSprite(decor, "rock"), OptSprite(decor, "rough"), OptSprite(decor, "bank"));
    }

    private static SpriteRule? OptSprite(TomlTable decor, string key)
    {
        if (!decor.TryGetValue(key, out object? v)) return null;
        if (v is not string kind || !Enum.TryParse(kind, true, out PropKind pk)) throw new InvalidDataException($"[decor] {key}: unknown sprite '{v}'");
        return new SpriteRule(pk, OptInt(decor, key + "_permille", 300, 0, 1000));
    }

    private static TerrainStyle ParseStyle(TomlTable root)
    {
        TerrainStyle st = TerrainStyle.None;
        if (root.TryGetValue("style", out object? sv) && sv is TomlTable t)
        {
            st = st with
            {
                BlotchShift = OptInt(t, "blotch_shift", st.BlotchShift, 1, 6),
                DensityFull = OptInt(t, "density_full", st.DensityFull, 1, 255),
                ToneAmp = OptInt(t, "tone_amp", st.ToneAmp, 0, 40),
                ToneCells = OptInt(t, "tone_cells", st.ToneCells, 1, 64),
                RoadEdge = OptColour(t, "road_edge"),
                RoadEdgeTexels = OptInt(t, "road_edge_texels", st.RoadEdgeTexels, 1, 8),
                Shore = OptColour(t, "shore"),
                ShoreTexels = OptInt(t, "shore_texels", st.ShoreTexels, 1, 8),
                CliffTop = OptColour(t, "cliff_top"),
                CliffTopTexels = OptInt(t, "cliff_top_texels", st.CliffTopTexels, 1, 8),
                CliffShadowPermille = OptInt(t, "cliff_shadow_permille", st.CliffShadowPermille, 0, 1000),
                CliffShadowTexels = OptInt(t, "cliff_shadow_texels", st.CliffShadowTexels, 1, 12),
            };
        }

        if (root.TryGetValue("field", out object? fv) && fv is TomlTable f)
        {
            st = st with
            {
                FieldRimTexels = OptInt(f, "rim_texels", st.FieldRimTexels, 0, 4),
                FieldBedPermille = OptInt(f, "bed_permille", st.FieldBedPermille, 0, 1000),
                FieldMaxHeaps = OptInt(f, "max_heaps", st.FieldMaxHeaps, 1, 8),
                Ashfall = OptField(f, "ashfall"),
                Pitchblende = OptField(f, "pitchblende"),
            };
        }

        return st;
    }

    private static FieldLook? OptField(TomlTable parent, string key)
    {
        if (!parent.TryGetValue(key, out object? v) || v is not TomlTable t) return null;
        return new FieldLook(Colour(t, "body"), Colour(t, "light"), Colour(t, "shade"), Colour(t, "rim"));
    }

    private static int OptInt(TomlTable parent, string key, int fallback, int min, int max)
    {
        if (!parent.ContainsKey(key)) return fallback;
        int v = Int(parent, key);
        if (v < min || v > max) throw new InvalidDataException($"palette key '{key}' must be {min}..{max}");
        return v;
    }

    private static Rgb? OptColour(TomlTable parent, string key) => parent.ContainsKey(key) ? Colour(parent, key) : null;

    private static TomlTable Table(TomlTable parent, string key) =>
        parent.TryGetValue(key, out object? v) && v is TomlTable t
            ? t
            : throw new InvalidDataException($"palette section [{key}] is missing");

    private static int Int(TomlTable parent, string key)
    {
        if (parent.TryGetValue(key, out object? v) && v is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
        throw new InvalidDataException($"palette key '{key}' is missing or not an integer");
    }

    private static int Permille(TomlTable parent, string key)
    {
        int p = Int(parent, key);
        if (p < 0 || p > 1000) throw new InvalidDataException($"palette key '{key}' must be 0..1000 permille");
        return p;
    }

    private static Rgb Colour(TomlTable parent, string key)
    {
        if (!parent.TryGetValue(key, out object? v) || v is not string s || s.Length != 7 || s[0] != '#')
            throw new InvalidDataException($"palette colour '{key}' must be a #RRGGBB string");
        uint rgb = uint.Parse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    /// <summary>An 8-bit RGB colour.</summary>
    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>The three shades one land type is drawn with.</summary>
    /// <param name="Dark">The darkest shade.</param>
    /// <param name="Base">The shade most texels use, and the colour a neighbour blends toward.</param>
    /// <param name="Light">The lightest shade.</param>
    public readonly record struct LandShades(Rgb Dark, Rgb Base, Rgb Light);

    /// <summary>How a resource overlay is drawn as a deposit: body colour, lit side, shaded side and the dark rim/contact shadow.</summary>
    /// <param name="Body">Main colour of a heap or crystal.</param>
    /// <param name="Light">Lit (north-west) side.</param>
    /// <param name="Shade">Shaded (south-east) side.</param>
    /// <param name="Rim">Dark colour of the field rim and of the contact shadow round each heap.</param>
    public sealed record FieldLook(Rgb Body, Rgb Light, Rgb Shade, Rgb Rim);

    /// <summary>
    /// Optional terrain-bake options, all off in <see cref="None"/> (which reproduces the original look byte for byte). Distances are in
    /// texels, so a palette that sets them also picks its <c>texel_cells</c>.
    /// </summary>
    public sealed record TerrainStyle
    {
        /// <summary>No options: the plain flat-colour-plus-noise look.</summary>
        public static readonly TerrainStyle None = new();

        /// <summary>The cell-scale shade blotch is 1 &lt;&lt; this many texels square (original: 3 = 8 texels).</summary>
        public int BlotchShift { get; init; } = 3;

        /// <summary>The density value that counts as a full (richest) cell. 255 = the original scale; the sim itself uses 12 (<c>EconomyState.MaxDensity</c>).</summary>
        public int DensityFull { get; init; } = 255;

        /// <summary>Largest large-scale tone offset in RGB levels on open ground (0 = off).</summary>
        public int ToneAmp { get; init; }

        /// <summary>Wavelength of the large-scale tone variation, in map cells.</summary>
        public int ToneCells { get; init; } = 6;

        /// <summary>Colour of the line along the edge of a road cell next to a non-road cell, or null for no line.</summary>
        public Rgb? RoadEdge { get; init; }

        /// <summary>Road edge line thickness in texels.</summary>
        public int RoadEdgeTexels { get; init; } = 1;

        /// <summary>Foam colour of the band on water and shallow cells next to land, or null for no shoreline.</summary>
        public Rgb? Shore { get; init; }

        /// <summary>Shore band width in texels.</summary>
        public int ShoreTexels { get; init; } = 2;

        /// <summary>Highlight colour on the north and west lips of a cliff cell (dark face on the south and east), or null for none.</summary>
        public Rgb? CliffTop { get; init; }

        /// <summary>Cliff highlight and face line thickness in texels.</summary>
        public int CliffTopTexels { get; init; } = 1;

        /// <summary>Darkening (permille, at the cliff foot) of the ground south and east of a cliff cell; 0 = no drop shadow.</summary>
        public int CliffShadowPermille { get; init; }

        /// <summary>Drop shadow length in texels.</summary>
        public int CliffShadowTexels { get; init; } = 3;

        /// <summary>Rim width in texels where a resource field meets other ground (0 = none).</summary>
        public int FieldRimTexels { get; init; } = 1;

        /// <summary>How far (permille) a resource cell's ground is tinted toward the rim colour, so the field reads as a patch.</summary>
        public int FieldBedPermille { get; init; }

        /// <summary>Heaps or crystals in a cell at full density (a cell at density 0 has one).</summary>
        public int FieldMaxHeaps { get; init; } = 4;

        /// <summary>Slag-heap look for ashfall; null keeps the speckle.</summary>
        public FieldLook? Ashfall { get; init; }

        /// <summary>Crystal look for pitchblende; null keeps the chunk speckle.</summary>
        public FieldLook? Pitchblende { get; init; }

        /// <summary>True when any shore, road-edge or cliff option is on.</summary>
        public bool HasEdgeEffects => RoadEdge is not null || Shore is not null || CliffTop is not null || CliffShadowPermille > 0;
    }
}
