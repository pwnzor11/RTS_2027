// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Inputs of <see cref="MapGenerator.Generate(MapGenParams)"/> (M4). The map depends only on these; Name null = the default
/// name <c>&lt;biome&gt;_&lt;s|m|l&gt;_&lt;players&gt;p_&lt;seed&gt;</c> (<see cref="MapGenerator.DefaultName"/>).</summary>
public sealed record MapGenParams(Biome Biome, MapSize Size, int Players, ulong Seed, string? Name = null);
