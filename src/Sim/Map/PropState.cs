// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Map;

/// <summary>
/// The "props" state part (M12-16, D-264 (3)): the health of every destructible prop (Tree, Snag, Wreck) on the map. Appended after
/// "commander_orders" (before extension parts). <see cref="Health"/> is one ushort per cell, row-major like the map; 0 = no prop with health
/// there (no prop, a Block or Pylon that never breaks, or any other overlay). It is seeded from the starting overlay and the
/// <see cref="PropRules"/> at setup, so a map without destructible props writes a header and a zero count.
/// Layout: version, width, height, then the number of cells with health followed by (cell index, health) pairs in ascending index order.
/// </summary>
public sealed class PropState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Health per cell (index = y * width + x); 0 = nothing destructible on the cell.</summary>
    public readonly ushort[] Health;

    private readonly int _width;
    private readonly int _height;

    /// <summary>Seeds the health of every destructible prop of the starting map.</summary>
    public PropState(MapData map, PropRules rules)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rules);
        _width = map.Width; _height = map.Height;
        Health = new ushort[_width * _height];
        for (int i = 0; i < Health.Length; i++)
            if (OverlayKinds.IsDestructible(map.Overlay[i])) Health[i] = (ushort)Math.Clamp(rules.HealthOf(map.Overlay[i]), 1, ushort.MaxValue);
    }

    /// <inheritdoc />
    public string Name => "props";

    /// <summary>Health left on a cell (0 = none).</summary>
    public int HealthAt(CellCoord c) => Health[c.Y * _width + c.X];

    /// <summary>Number of cells that carry health (diagnostics and tests).</summary>
    public int Count
    {
        get { int n = 0; foreach (ushort h in Health) if (h != 0) n++; return n; }
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(_width); w.Write(_height);
        w.Write(Count);
        for (int i = 0; i < Health.Length; i++)
            if (Health[i] != 0) { w.Write(i); w.Write(Health[i]); }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("props version mismatch.");
        if (r.ReadInt32() != _width || r.ReadInt32() != _height) throw new InvalidDataException("props map size mismatch.");
        Array.Clear(Health);
        int n = StateIo.Count(r, Name);
        for (int k = 0; k < n; k++)
        {
            int i = r.ReadInt32();
            if (i < 0 || i >= Health.Length) throw new InvalidDataException("props cell out of range.");
            Health[i] = r.ReadUInt16();
        }
    }
}
