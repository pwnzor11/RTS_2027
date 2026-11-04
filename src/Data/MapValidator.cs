// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Checks a map for skirmish use (design 08 §5; shared by the generator, the editor and <c>map validate</c>).
/// OWNER: M4-02. Stub (M4-01): finds nothing.</summary>
public static class MapValidator
{
    /// <summary>Every issue found, errors and warnings, in a fixed order. <paramref name="terrain"/> null = the built-in conservative
    /// passability rule.</summary>
    public static IReadOnlyList<MapIssue> Validate(MapData map, MapMeta? meta, TerrainTable? terrain)
    {
        ArgumentNullException.ThrowIfNull(map);
        // M4-02 fills
        return Array.Empty<MapIssue>();
    }
}
