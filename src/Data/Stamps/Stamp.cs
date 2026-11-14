// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data.Stamps;

/// <summary>A hand-drawn prefab block of map cells (M12-13, design 16 G3), loaded from <c>data/stamps/&lt;id&gt;.toml</c>. <paramref name="Biomes"/>
/// holds biome keys (empty = any); <paramref name="Rows"/> are the drawn rows and <paramref name="Cells"/> the same grid resolved through the
/// legend, row-major, <c>Width * Height</c> entries.</summary>
public sealed record Stamp(string Id, string Name, int Width, int Height, IReadOnlyList<string> Biomes, IReadOnlyList<string> Rows, IReadOnlyList<StampCell> Cells);
