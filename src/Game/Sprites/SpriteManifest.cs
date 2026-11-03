// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Numerics;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Sprites;

/// <summary>
/// A parsed <c>&lt;id&gt;.sprite.toml</c>: the sheet PNG beside it, the frame size, the origin pixel and the
/// animation states. The id is the file stem, so <c>tank_v2.sprite.toml</c> is the sprite <c>tank_v2</c>, which is
/// what rules data will refer to.
/// </summary>
/// <remarks>
/// Origin: for a vehicle it is the pixel that sits on the ground contact point (the cell centre), counted from the
/// frame's top-left corner. For a building it is the top-left pixel of the footprint's top-left cell. The renderer
/// places that pixel on the world position and draws the frame around it.
/// Unknown keys are errors, so a misspelt key cannot silently fall back to a default.
/// </remarks>
public sealed class SpriteManifest
{
    private static readonly int[] AllowedFacings = { 1, 8, 16, 32 };
    private static readonly string[] TopKeys = { "file", "frame_w", "frame_h", "origin_x", "origin_y", "house", "state", "fx" };
    private static readonly string[] StateKeys = { "name", "facings", "frames", "first", "ticks_per_frame", "loop" };
    private static readonly string[] FxKeys = { "state", "kind", "x", "y", "phase" };

    private SpriteManifest(string id, string sourcePath, string sheetFile, int frameW, int frameH, int originX, int originY,
        uint[] houseRamp, IReadOnlyList<SpriteState> states, IReadOnlyList<FxAnchor> fx)
    {
        Id = id;
        SourcePath = sourcePath;
        SheetFile = sheetFile;
        FrameW = frameW;
        FrameH = frameH;
        OriginX = originX;
        OriginY = originY;
        HouseRamp = houseRamp;
        States = states;
        Fx = fx;
    }

    /// <summary>Sprite id, the manifest's file stem.</summary>
    public string Id { get; }

    /// <summary>Full path of the manifest file this came from, for error messages and loading the PNG.</summary>
    public string SourcePath { get; }

    /// <summary>Sheet PNG name, relative to the manifest's folder.</summary>
    public string SheetFile { get; }

    /// <summary>Width of one frame in pixels.</summary>
    public int FrameW { get; }

    /// <summary>Height of one frame in pixels.</summary>
    public int FrameH { get; }

    /// <summary>X of the origin pixel inside each frame, from the left edge. See the type remarks.</summary>
    public int OriginX { get; }

    /// <summary>Y of the origin pixel inside each frame, from the top edge. See the type remarks.</summary>
    public int OriginY { get; }

    /// <summary>House ramp shades as 0xRRGGBB, darkest first. Empty when the sprite has no house paint.</summary>
    public IReadOnlyList<uint> HouseRamp { get; }

    /// <summary>Animation states in file order.</summary>
    public IReadOnlyList<SpriteState> States { get; }

    /// <summary>Effect anchors from the optional <c>[[fx]]</c> tables, in file order. Empty when there are none.</summary>
    public IReadOnlyList<FxAnchor> Fx { get; }

    /// <summary>The effect anchors of one state, in file order.</summary>
    /// <param name="state">State name.</param>
    public IEnumerable<FxAnchor> FxFor(string state) =>
        Fx.Where(a => string.Equals(a.State, state, StringComparison.Ordinal));

    /// <summary>Reads and parses a manifest file. The id is the file name without <c>.sprite.toml</c>.</summary>
    /// <exception cref="InvalidDataException">The file is missing a field, has a bad value or an unknown key.</exception>
    public static SpriteManifest Load(string path)
    {
        string name = Path.GetFileName(path);
        const string suffix = ".sprite.toml";
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{path}: sprite manifests are named <id>.sprite.toml");
        }
        return Parse(File.ReadAllText(path), name[..^suffix.Length], path);
    }

    /// <summary>Parses manifest text.</summary>
    /// <param name="text">TOML text.</param>
    /// <param name="id">Sprite id (the file stem).</param>
    /// <param name="sourcePath">Where the text came from, used in error messages.</param>
    /// <exception cref="InvalidDataException">A field is missing, has a bad value, or a state block overlaps another.</exception>
    public static SpriteManifest Parse(string text, string id, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException($"{sourcePath}: sprite id is empty");

        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text)
                ?? throw new InvalidDataException($"{sourcePath}: empty sprite manifest");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException($"{sourcePath}: not valid TOML: {ex.Message}", ex);
        }

        RejectUnknown(root, TopKeys, sourcePath, "top level");

        string file = Text(root, "file", sourcePath);
        int frameW = Positive(root, "frame_w", sourcePath);
        int frameH = Positive(root, "frame_h", sourcePath);
        int originX = Int(root, "origin_x", sourcePath);
        int originY = Int(root, "origin_y", sourcePath);
        if (originX < 0 || originX > frameW || originY < 0 || originY > frameH)
        {
            throw new InvalidDataException(
                $"{sourcePath}: origin ({originX}, {originY}) must lie inside the {frameW}x{frameH} frame");
        }

        uint[] ramp = ReadHouse(root, sourcePath);
        IReadOnlyList<SpriteState> states = ReadStates(root, sourcePath);

        IReadOnlyList<FxAnchor> fx = ReadFx(root, states, frameW, frameH, sourcePath);

        return new SpriteManifest(id, sourcePath, file, frameW, frameH, originX, originY, ramp, states, fx);
    }

    /// <summary>
    /// The sheet frame to show for a state, a facing and a simulation tick. The facing is the 0..255 byte the sim
    /// uses, rounded to the state's facing count. Animation advances every <c>ticks_per_frame</c> ticks and wraps.
    /// </summary>
    /// <param name="state">State name.</param>
    /// <param name="facing">Facing byte, 0 = north, clockwise.</param>
    /// <param name="tick">Simulation tick, zero or more.</param>
    /// <exception cref="KeyNotFoundException">The sprite has no state with that name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tick is negative.</exception>
    public int FrameIndex(string state, byte facing, int tick)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "Ticks start at 0.");
        SpriteState s = State(state);
        int facingIndex = s.Facings == 1 ? 0 : Facing.ToFacings(facing, s.Facings);
        int raw = tick / s.TicksPerFrame;
        int step = s.Loop ? raw % s.Frames : Math.Min(raw, s.Frames - 1);
        return s.First + facingIndex * s.Frames + step;
    }

    /// <summary>Finds a state by name.</summary>
    /// <exception cref="KeyNotFoundException">The sprite has no state with that name. The message lists the names it has.</exception>
    public SpriteState State(string name)
    {
        foreach (SpriteState state in States)
        {
            if (string.Equals(state.Name, name, StringComparison.Ordinal)) return state;
        }
        string known = string.Join(", ", States.Select(s => s.Name));
        throw new KeyNotFoundException($"sprite '{Id}' has no state '{name}' (states: {known})");
    }

    private static uint[] ReadHouse(TomlTable root, string sourcePath)
    {
        if (!root.TryGetValue("house", out object? raw))
        {
            return Array.Empty<uint>();
        }
        if (raw is not TomlTable house)
        {
            throw new InvalidDataException($"{sourcePath}: [house] must be a table");
        }
        RejectUnknown(house, new[] { "ramp" }, sourcePath, "[house]");
        if (!house.TryGetValue("ramp", out object? rampRaw) || rampRaw is not TomlArray array)
        {
            throw new InvalidDataException($"{sourcePath}: [house] needs ramp = [\"#RRGGBB\", ...]");
        }
        if (array.Count != PlayerColours.RampSteps)
        {
            throw new InvalidDataException(
                $"{sourcePath}: [house] ramp must have {PlayerColours.RampSteps} colours, found {array.Count}");
        }

        var ramp = new uint[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not string hex || hex.Length != 7 || hex[0] != '#'
                || !uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            {
                throw new InvalidDataException($"{sourcePath}: ramp entry {i} must be #RRGGBB, found {array[i]}");
            }
            ramp[i] = rgb;
        }
        return ramp;
    }

    private static IReadOnlyList<SpriteState> ReadStates(TomlTable root, string sourcePath)
    {
        if (!root.TryGetValue("state", out object? raw) || raw is not TomlTableArray blocks || blocks.Count == 0)
        {
            throw new InvalidDataException($"{sourcePath}: needs at least one [[state]] table");
        }

        var states = new List<SpriteState>(blocks.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        int cursor = 0;
        foreach (TomlTable block in blocks)
        {
            RejectUnknown(block, StateKeys, sourcePath, "[[state]]");
            string name = Text(block, "name", sourcePath);
            if (!names.Add(name))
            {
                throw new InvalidDataException($"{sourcePath}: state '{name}' is defined twice");
            }

            int facings = Int(block, "facings", sourcePath);
            if (Array.IndexOf(AllowedFacings, facings) < 0)
            {
                throw new InvalidDataException(
                    $"{sourcePath}: state '{name}' facings must be 1, 8, 16 or 32, found {facings}");
            }
            int frames = Optional(block, "frames", 1, sourcePath);
            if (frames < 1) throw new InvalidDataException($"{sourcePath}: state '{name}' frames must be at least 1");
            int ticks = Optional(block, "ticks_per_frame", 1, sourcePath);
            if (ticks < 1) throw new InvalidDataException($"{sourcePath}: state '{name}' ticks_per_frame must be at least 1");

            // first defaults to the cell after the previous state, so states pack in file order.
            int first = Optional(block, "first", cursor, sourcePath);
            bool loop = true;
            if (block.TryGetValue("loop", out object? loopRaw))
            {
                loop = loopRaw as bool? ?? throw new InvalidDataException(
                    $"{sourcePath}: state '{name}' loop must be true or false, found {loopRaw}");
            }
            var state = new SpriteState(name, facings, frames, first, ticks, loop);
            states.Add(state);
            cursor = first + state.CellCount;
        }

        for (int i = 0; i < states.Count; i++)
        {
            for (int j = i + 1; j < states.Count; j++)
            {
                SpriteState a = states[i];
                SpriteState b = states[j];
                bool overlap = a.First < b.First + b.CellCount && b.First < a.First + a.CellCount;
                if (overlap)
                {
                    throw new InvalidDataException(
                        $"{sourcePath}: state '{a.Name}' and state '{b.Name}' use the same frames");
                }
            }
        }
        return states;
    }

    private static IReadOnlyList<FxAnchor> ReadFx(TomlTable root, IReadOnlyList<SpriteState> states, int frameW, int frameH,
        string sourcePath)
    {
        if (!root.TryGetValue("fx", out object? raw))
        {
            return Array.Empty<FxAnchor>();
        }
        if (raw is not TomlTableArray blocks)
        {
            throw new InvalidDataException($"{sourcePath}: fx must be written as [[fx]] tables");
        }

        var list = new List<FxAnchor>(blocks.Count);
        foreach (TomlTable block in blocks)
        {
            RejectUnknown(block, FxKeys, sourcePath, "[[fx]]");
            string state = Text(block, "state", sourcePath);
            if (!states.Any(s => string.Equals(s.Name, state, StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"{sourcePath}: [[fx]] refers to unknown state '{state}'");
            }
            string kind = Text(block, "kind", sourcePath);
            int x = Int(block, "x", sourcePath);
            int y = Int(block, "y", sourcePath);
            if (x > frameW || y > frameH)
            {
                throw new InvalidDataException(
                    $"{sourcePath}: [[fx]] '{kind}' anchor ({x}, {y}) must lie inside the {frameW}x{frameH} frame");
            }
            list.Add(new FxAnchor(state, kind, x, y, Optional(block, "phase", 0, sourcePath)));
        }
        return list;
    }

    private static void RejectUnknown(TomlTable table, string[] allowed, string sourcePath, string where)
    {
        foreach (string key in table.Keys)
        {
            if (Array.IndexOf(allowed, key) < 0)
            {
                throw new InvalidDataException($"{sourcePath}: unknown key '{key}' in {where}");
            }
        }
    }

    private static string Text(TomlTable table, string key, string sourcePath)
    {
        if (table.TryGetValue(key, out object? raw))
        {
            if (raw is string text && text.Trim().Length > 0) return text;
            throw new InvalidDataException($"{sourcePath}: '{key}' must be a non-empty string");
        }
        throw new InvalidDataException($"{sourcePath}: missing '{key}'");
    }

    private static int Int(TomlTable table, string key, string sourcePath)
    {
        if (!table.TryGetValue(key, out object? raw))
        {
            throw new InvalidDataException($"{sourcePath}: missing '{key}'");
        }
        if (raw is long value && value >= 0 && value <= int.MaxValue) return (int)value;
        throw new InvalidDataException($"{sourcePath}: '{key}' must be a whole number of 0 or more, found {raw}");
    }

    private static int Positive(TomlTable table, string key, string sourcePath)
    {
        int value = Int(table, key, sourcePath);
        if (value == 0) throw new InvalidDataException($"{sourcePath}: '{key}' must be greater than 0");
        return value;
    }

    private static int Optional(TomlTable table, string key, int fallback, string sourcePath) =>
        table.ContainsKey(key) ? Int(table, key, sourcePath) : fallback;
}
